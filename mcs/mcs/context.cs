﻿//
// context.cs: Various compiler contexts.
//
// Author:
//   Marek Safar (marek.safar@gmail.com)
//   Miguel de Icaza (miguel@ximian.com)
//
// Copyright 2001, 2002, 2003 Ximian, Inc.
// Copyright 2004-2009 Novell, Inc.
// Copyright 2011 Xamarin Inc.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Mono.CSharp
{
	public enum LookupMode
	{
		Normal = 0,
		Probing = 1,
		IgnoreAccessibility = 2
	}

	//
	// Implemented by elements which can act as independent contexts
	// during resolve phase. Used mostly for lookups.
	//
	public interface IMemberContext : IModuleContext
	{
		//
		// A scope type context, it can be inflated for generic types
		//
		TypeSpec CurrentType { get; }

		//
		// A scope type parameters either VAR or MVAR
		//
		TypeParameters CurrentTypeParameters { get; }

		//
		// A member definition of the context. For partial types definition use
		// CurrentTypeDefinition.PartialContainer otherwise the context is local
		//
		// TODO: Obsolete it in this context, dynamic context cannot guarantee sensible value
		//
		MemberCore CurrentMemberDefinition { get; }

		bool IsObsolete { get; }
		bool IsUnsafe { get; }
		bool IsStatic { get; }

		string GetSignatureForError ();

		ExtensionMethodCandidates LookupExtensionMethod (TypeSpec extensionType, string name, int arity);
		FullNamedExpression LookupNamespaceOrType (string name, int arity, LookupMode mode, bool absolute_ns, Location loc);
		FullNamedExpression LookupNamespaceAlias (string name);
	}

	public interface IModuleContext
	{
		ModuleContainer Module { get; }
	}

	//
	// Block or statement resolving context
	//
	public class BlockContext : ResolveContext
	{
		readonly TypeSpec return_type;

		//
		// Tracks the last offset used by VariableInfo
		//
		public int AssignmentInfoOffset;

		public BlockContext (IMemberContext mc, ExplicitBlock block, TypeSpec returnType)
			: base (mc)
		{
			if (returnType == null)
				throw new ArgumentNullException ("returnType");

			this.return_type = returnType;

			// TODO: check for null value
			CurrentBlock = block;
		}

		public BlockContext (ResolveContext rc, ExplicitBlock block, TypeSpec returnType)
			: this (rc.MemberContext, block, returnType)
		{
			if (rc.IsUnsafe)
				flags |= ResolveContext.Options.UnsafeScope;

			if (rc.HasSet (ResolveContext.Options.CheckedScope))
				flags |= ResolveContext.Options.CheckedScope;

			if (!rc.ConstantCheckState)
				flags &= ~Options.ConstantCheckState;

			if (rc.IsInProbingMode)
				flags |= ResolveContext.Options.ProbingMode;

			if (rc.HasSet (ResolveContext.Options.FieldInitializerScope))
				flags |= ResolveContext.Options.FieldInitializerScope;

			if (rc.HasSet (ResolveContext.Options.ExpressionTreeConversion))
				flags |= ResolveContext.Options.ExpressionTreeConversion;

			if (rc.HasSet (ResolveContext.Options.BaseInitializer))
				flags |= ResolveContext.Options.BaseInitializer;
		}

		public ExceptionStatement CurrentTryBlock { get; set; }

		public LoopStatement EnclosingLoop { get; set; }

		public LoopStatement EnclosingLoopOrSwitch { get; set; }

		public Switch Switch { get; set; }

		public TypeSpec ReturnType {
			get { return return_type; }
		}
	}

	//
	// Expression resolving context
	//
	public class ResolveContext : IMemberContext
	{
		[Flags]
		public enum Options
		{
			/// <summary>
			///   This flag tracks the `checked' state of the compilation,
			///   it controls whether we should generate code that does overflow
			///   checking, or if we generate code that ignores overflows.
			///
			///   The default setting comes from the command line option to generate
			///   checked or unchecked code plus any source code changes using the
			///   checked/unchecked statements or expressions.   Contrast this with
			///   the ConstantCheckState flag.
			/// </summary>
			CheckedScope = 1 << 0,

			/// <summary>
			///   The constant check state is always set to `true' and cant be changed
			///   from the command line.  The source code can change this setting with
			///   the `checked' and `unchecked' statements and expressions. 
			/// </summary>
			ConstantCheckState = 1 << 1,

			AllCheckStateFlags = CheckedScope | ConstantCheckState,

			//
			// unsafe { ... } scope
			//
			UnsafeScope = 1 << 2,
			CatchScope = 1 << 3,
			FinallyScope = 1 << 4,
			FieldInitializerScope = 1 << 5,
			CompoundAssignmentScope = 1 << 6,
			FixedInitializerScope = 1 << 7,
			BaseInitializer = 1 << 8,

			//
			// Inside an enum definition, we do not resolve enumeration values
			// to their enumerations, but rather to the underlying type/value
			// This is so EnumVal + EnumValB can be evaluated.
			//
			// There is no "E operator + (E x, E y)", so during an enum evaluation
			// we relax the rules
			//
			EnumScope = 1 << 9,

			ConstantScope = 1 << 10,

			ConstructorScope = 1 << 11,

			UsingInitializerScope = 1 << 12,

			LockScope = 1 << 13,

			TryScope = 1 << 14,

			TryWithCatchScope = 1 << 15,

			ConditionalAccessReceiver = 1 << 16,

			///
			/// Indicates the current context is in probing mode, no errors are reported. 
			///
			ProbingMode = 1 << 22,

			//
			// Return and ContextualReturn statements will set the ReturnType
			// value based on the expression types of each return statement
			// instead of the method return type which is initially null.
			//
			InferReturnType = 1 << 23,

			OmitDebuggingInfo = 1 << 24,

			ExpressionTreeConversion = 1 << 25,

			InvokeSpecialName = 1 << 26,

			PsExtended = 1 << 27,

			PsDynamicDisabled = 1 << 28,

			HasNoReturnType = 1 << 29,
		}

		// utility helper for CheckExpr, UnCheckExpr, Checked and Unchecked statements
		// it's public so that we can use a struct at the callsite
		public struct FlagsHandle : IDisposable
		{
			readonly ResolveContext ec;
			readonly Options invmask, oldval;

			public FlagsHandle (ResolveContext ec, Options flagsToSet)
				: this (ec, flagsToSet, flagsToSet)
			{
			}

			internal FlagsHandle (ResolveContext ec, Options mask, Options val)
			{
				this.ec = ec;
				invmask = ~mask;
				oldval = ec.flags & mask;
				ec.flags = (ec.flags & invmask) | (val & mask);

//				if ((mask & Options.ProbingMode) != 0)
//					ec.Report.DisableReporting ();
			}

			public void Dispose ()
			{
//				if ((invmask & Options.ProbingMode) == 0)
//					ec.Report.EnableReporting ();

				ec.flags = (ec.flags & invmask) | oldval;
			}
		}

		protected Options flags;

		protected SourceFileType fileType;

		//
		// Whether we are inside an anonymous method.
		//
		public AnonymousExpression CurrentAnonymousMethod;

		//
		// Holds a varible used during collection or object initialization.
		//
		public Expression CurrentInitializerVariable;

		public Block CurrentBlock;

		public readonly IMemberContext MemberContext;


		//83aec9d3	M	Ben Cooley	06/25/2013	Activated inliner.  Only functions work.. void functions will fail.			///   If this is non-null, points to the current statement
		/// <summary>
		///   If this is non-null, points to the current statement
		/// </summary>
		public Statement Statement;

		public ResolveContext (IMemberContext mc)
		{
			if (mc == null)
				throw new ArgumentNullException ();

			MemberContext = mc;

			//
			// The default setting comes from the command line option
			//
			if (mc.Module.Compiler.Settings.Checked)
				flags |= Options.CheckedScope;

			//
			// The constant check state is always set to true
			//
			flags |= Options.ConstantCheckState;

			//
			// File type set from member context module sourcefile.
			//
			var memberCore = mc as MemberCore;
			if (memberCore != null && memberCore.Location.SourceFile != null) {
				fileType = memberCore.Location.SourceFile.FileType;
			} else if (mc.Module != null && mc.Module.Location.SourceFile != null) {
				fileType = mc.Module.Location.SourceFile.FileType;
				if (mc.Module.Location.SourceFile.PsExtended)
					flags |= Options.PsExtended;
			} else {
				fileType = SourceFileType.CSharp;
			}

			//
			// Set dynamic enabled state
			//
			if (memberCore is MethodCore) {
				if (!(((MethodCore)mc).AllowDynamic ?? true))
					flags |= Options.PsDynamicDisabled;
			} else if (memberCore is TypeDefinition) {
				if (!(((TypeDefinition)mc).AllowDynamic ?? true))
					flags |= Options.PsDynamicDisabled;
			} else if (memberCore != null && memberCore.Parent != null) {
				if (!(memberCore.Parent.AllowDynamic ?? true))
					flags |= Options.PsDynamicDisabled;
			} else {
				if (!Module.Compiler.Settings.AllowDynamic)
					flags |= Options.PsDynamicDisabled;
			}

			//
			// Handle missing return type
			//
			if (memberCore is Method) {
				if (((Method)memberCore).HasNoReturnType)
					flags |= Options.HasNoReturnType;
			}
		}

		public ResolveContext (IMemberContext mc, Options options)
			: this (mc)
		{
			flags |= options;
		}

		#region Properties

		public BuiltinTypes BuiltinTypes {
			get {
				return MemberContext.Module.Compiler.BuiltinTypes;
			}
		}

		public virtual ExplicitBlock ConstructorBlock {
			get {
				return CurrentBlock.Explicit;
			}
		}

		//
		// The current iterator
		//
		public Iterator CurrentIterator {
			get { return CurrentAnonymousMethod as Iterator; }
		}

		public TypeSpec CurrentType {
			get { return MemberContext.CurrentType; }
		}

		public TypeParameters CurrentTypeParameters {
			get { return MemberContext.CurrentTypeParameters; }
		}

		public MemberCore CurrentMemberDefinition {
			get { return MemberContext.CurrentMemberDefinition; }
		}

		public bool ConstantCheckState {
			get { return (flags & Options.ConstantCheckState) != 0; }
		}

		public bool IsInProbingMode {
			get {
				return (flags & Options.ProbingMode) != 0;
			}
		}

		public bool IsObsolete {
			get {
				// Disables obsolete checks when probing is on
				return MemberContext.IsObsolete;
			}
		}

		public bool IsStatic {
			get {
				return MemberContext.IsStatic;
			}
		}

		public bool IsUnsafe {
			get {
				return HasSet (Options.UnsafeScope) || MemberContext.IsUnsafe;
			}
		}

		public bool AllowDynamic {
			get {
				return (flags & Options.PsDynamicDisabled) == 0;
			}
		}

		public bool IsRuntimeBinder {
			get {
				return Module.Compiler.IsRuntimeBinder;
			}
		}

		public bool IsVariableCapturingRequired {
			get {
				return !IsInProbingMode;
			}
		}

		public bool HasNoReturnType {
			get {
				return (flags & Options.HasNoReturnType) != 0;
			}
		}

		public ModuleContainer Module {
			get {
				return MemberContext.Module;
			}
		}

		public Report Report {
			get {
				return Module.Compiler.Report;
			}
		}

		public SourceFileType FileType {
			get { return fileType; }
			set { fileType = value; }
		}

		public bool PsExtended {
			get { return (flags & Options.PsExtended) != 0; }
			set { 
				if (value) 
					flags |= Options.PsExtended; 
				else 
					flags &= ~Options.PsExtended; 
			}
		}

		public bool PsNumberIsFloat {
			get {
				if (CurrentMemberDefinition != null && CurrentMemberDefinition.Parent != null) {
					var attributes = CurrentMemberDefinition.Parent.OptAttributes;
					if (attributes != null && attributes.Contains (Module.PredefinedAttributes.NumberIsFloatAttribute))
						return true;
				}
				return false;
			}
		}

		public Target Target {
			get { return Module.Compiler.Settings.Target; }
		}

		#endregion

		public bool MustCaptureVariable (INamedBlockVariable local)
		{
			if (CurrentAnonymousMethod == null)
				return false;

			//
			// Capture only if this or any of child blocks contain yield
			// or it's a parameter
			//
			if (CurrentAnonymousMethod.IsIterator)
				return local.IsParameter || local.Block.Explicit.HasYield;

			//
			// Capture only if this or any of child blocks contain await
			// or it's a parameter or we need to access variable from 
			// different parameter block
			//
			if (CurrentAnonymousMethod is AsyncInitializer)
				return local.IsParameter || local.Block.Explicit.HasAwait || CurrentBlock.Explicit.HasAwait ||
					local.Block.ParametersBlock != CurrentBlock.ParametersBlock.Original;

			return local.Block.ParametersBlock != CurrentBlock.ParametersBlock.Original;
		}

		public bool HasSet (Options options)
		{
			return (this.flags & options) == options;
		}

		public bool HasAny (Options options)
		{
			return (this.flags & options) != 0;
		}


		// Temporarily set all the given flags to the given value.  Should be used in an 'using' statement
		public FlagsHandle Set (Options options)
		{
			return new FlagsHandle (this, options);
		}

		public FlagsHandle With (Options options, bool enable)
		{
			return new FlagsHandle (this, options, enable ? options : 0);
		}

		#region IMemberContext Members

		public string GetSignatureForError ()
		{
			return MemberContext.GetSignatureForError ();
		}

		public ExtensionMethodCandidates LookupExtensionMethod (TypeSpec extensionType, string name, int arity)
		{
			return MemberContext.LookupExtensionMethod (extensionType, name, arity);
		}

		public FullNamedExpression LookupNamespaceOrType (string name, int arity, LookupMode mode, bool absolute_ns, Location loc)
		{
			return MemberContext.LookupNamespaceOrType (name, arity, mode, absolute_ns, loc);
		}

		public FullNamedExpression LookupNamespaceAlias (string name)
		{
			return MemberContext.LookupNamespaceAlias (name);
		}

		#endregion
	}

	public class FlowAnalysisContext
	{
		readonly CompilerContext ctx;

		public FlowAnalysisContext (CompilerContext ctx, ParametersBlock parametersBlock, int definiteAssignmentLength)
		{
			this.ctx = ctx;
			this.ParametersBlock = parametersBlock;

			DefiniteAssignment = definiteAssignmentLength == 0 ?
				DefiniteAssignmentBitSet.Empty :
				new DefiniteAssignmentBitSet (definiteAssignmentLength);
		}

		public DefiniteAssignmentBitSet DefiniteAssignment { get; set; }

		public DefiniteAssignmentBitSet DefiniteAssignmentOnTrue { get; set; }

		public DefiniteAssignmentBitSet DefiniteAssignmentOnFalse { get; set; }

		public List<LabeledStatement> LabelStack { get; set; }

		public ParametersBlock ParametersBlock { get; set; }

		public Report Report {
			get {
				return ctx.Report;
			}
		}

		public DefiniteAssignmentBitSet SwitchInitialDefinitiveAssignment { get; set; }

		public TryFinally TryFinally { get; set; }

		public bool UnreachableReported { get; set; }

		public DefiniteAssignmentBitSet BranchDefiniteAssignment ()
		{
			var dat = DefiniteAssignment;
			if (dat != DefiniteAssignmentBitSet.Empty)
				DefiniteAssignment = new DefiniteAssignmentBitSet (dat);
			return dat;
		}

		public bool IsDefinitelyAssigned (VariableInfo variable)
		{
			return variable.IsAssigned (DefiniteAssignment);
		}

		public bool IsStructFieldDefinitelyAssigned (VariableInfo variable, string name)
		{
			return variable.IsStructFieldAssigned (DefiniteAssignment, name);
		}

		public void SetVariableAssigned (VariableInfo variable, bool generatedAssignment = false)
		{
			variable.SetAssigned (DefiniteAssignment, generatedAssignment);
		}

		public void SetStructFieldAssigned (VariableInfo variable, string name)
		{
			variable.SetStructFieldAssigned (DefiniteAssignment, name);
		}
	}


	//
	// This class is used during the Statement.Clone operation
	// to remap objects that have been cloned.
	//
	// Since blocks are cloned by Block.Clone, we need a way for
	// expressions that must reference the block to be cloned
	// pointing to the new cloned block.
	//
	public class CloneContext
	{
		Dictionary<Block, Block> block_map = new Dictionary<Block, Block> ();

		public void AddBlockMap (Block from, Block to)
		{
			block_map.Add (from, to);
		}

		public Block LookupBlock (Block from)
		{
			Block result;
			if (!block_map.TryGetValue (from, out result)) {
				result = (Block) from.Clone (this);
			}

			return result;
		}

		///
		/// Remaps block to cloned copy if one exists.
		///
		public Block RemapBlockCopy (Block from)
		{
			Block mapped_to;
			if (!block_map.TryGetValue (from, out mapped_to))
				return from;

			return mapped_to;
		}
	}

	//
	// Main compiler context
	//
	public class CompilerContext
	{
		static readonly TimeReporter DisabledTimeReporter = new TimeReporter (false);

		readonly Report report;
		readonly BuiltinTypes builtin_types;
		readonly CompilerSettings settings;

		Dictionary<string, SourceFile> all_source_files;

		public CompilerContext (CompilerSettings settings, ReportPrinter reportPrinter)
		{
			this.settings = settings;
			this.report = new Report (this, reportPrinter);
			this.builtin_types = new BuiltinTypes ();
			this.TimeReporter = DisabledTimeReporter;
		}

		#region Properties

		public BuiltinTypes BuiltinTypes {
			get {
				return builtin_types;
			}
		}

		// Used for special handling of runtime dynamic context mostly
		// by error reporting but also by member accessibility checks
		public bool IsRuntimeBinder {
			get; set;
		}

		public Report Report {
			get {
				return report;
			}
		}

		public CompilerSettings Settings {
			get {
				return settings;
			}
		}

		public List<SourceFile> SourceFiles {
			get {
				return settings.SourceFiles;
			}
		}

		internal TimeReporter TimeReporter {
			get; set;
		}

		#endregion

		//
		// This is used when we encounter a #line preprocessing directive during parsing
		// to register additional source file names
		//
		public SourceFile LookupFile (CompilationSourceFile comp_unit, string name)
		{
			if (all_source_files == null) {
				all_source_files = new Dictionary<string, SourceFile> ();
				foreach (var source in SourceFiles)
					all_source_files[source.FullPathName] = source;
			}

			string path;
			if (!Path.IsPathRooted (name)) {
				var loc = comp_unit.SourceFile;
				string root = Path.GetDirectoryName (loc.FullPathName);
				path = Path.GetFullPath (Path.Combine (root, name));
				var dir = Path.GetDirectoryName (loc.Name);
				if (!string.IsNullOrEmpty (dir))
					name = Path.Combine (dir, name);
			} else
				path = name;

			SourceFile retval;
			if (all_source_files.TryGetValue (path, out retval))
				return retval;

			retval = new SourceFile (name, path, all_source_files.Count + 1);
			Location.AddFile (retval);
			all_source_files.Add (path, retval);
			return retval;
		}
	}

	//
	// Generic code emitter context
	//
	public class BuilderContext
	{
		[Flags]
		public enum Options
		{
			/// <summary>
			///   This flag tracks the `checked' state of the compilation,
			///   it controls whether we should generate code that does overflow
			///   checking, or if we generate code that ignores overflows.
			///
			///   The default setting comes from the command line option to generate
			///   checked or unchecked code plus any source code changes using the
			///   checked/unchecked statements or expressions.   Contrast this with
			///   the ConstantCheckState flag.
			/// </summary>
			CheckedScope = 1 << 0,

			AccurateDebugInfo = 1 << 1,

			OmitDebugInfo = 1 << 2,

			ConstructorScope = 1 << 3,

			AsyncBody = 1 << 4,
		}

		// utility helper for CheckExpr, UnCheckExpr, Checked and Unchecked statements
		// it's public so that we can use a struct at the callsite
		public struct FlagsHandle : IDisposable
		{
			readonly BuilderContext ec;
			readonly Options invmask, oldval;

			public FlagsHandle (BuilderContext ec, Options flagsToSet)
				: this (ec, flagsToSet, flagsToSet)
			{
			}

			internal FlagsHandle (BuilderContext ec, Options mask, Options val)
			{
				this.ec = ec;
				invmask = ~mask;
				oldval = ec.flags & mask;
				ec.flags = (ec.flags & invmask) | (val & mask);
			}

			public void Dispose ()
			{
				ec.flags = (ec.flags & invmask) | oldval;
			}
		}

		protected Options flags;

		public bool HasSet (Options options)
		{
			return (this.flags & options) == options;
		}

		// Temporarily set all the given flags to the given value.  Should be used in an 'using' statement
		public FlagsHandle With (Options options, bool enable)
		{
			return new FlagsHandle (this, options, enable ? options : 0);
		}
	}

	//
	// Parser session objects. We could recreate all these objects for each parser
	// instance but the best parser performance the session object can be reused
	//
	public class ParserSession
	{
		MD5 md5;

		public readonly char[] StreamReaderBuffer = new char[SeekableStreamReader.DefaultReadAheadSize * 2];
		public readonly Dictionary<char[], string>[] Identifiers = new Dictionary<char[], string>[Tokenizer.MaxIdentifierLength + 1];
		public readonly List<Parameter> ParametersStack = new List<Parameter> (4);
		public readonly char[] IDBuilder = new char[Tokenizer.MaxIdentifierLength];
		public readonly char[] NumberBuilder = new char[Tokenizer.MaxNumberLength];

		public LocationsBag LocationsBag { get; set; }
		public bool UseJayGlobalArrays { get; set; }
		public LocatedToken[] LocatedTokens { get; set; }
		public Mono.PlayScript.LocatedToken[] AsLocatedTokens { get; set; }

		public MD5 GetChecksumAlgorithm ()
		{
			return md5 ?? (md5 = MD5.Create ());
		}
	}
}
