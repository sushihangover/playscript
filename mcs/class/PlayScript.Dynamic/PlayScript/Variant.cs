//
// Copyright 2013 Zynga Inc.
//	
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//		
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.

#if PLATFORM_MONOTOUCH || PLATFORM_MONODROID
#define ALIGN32
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Diagnostics;
using PlayScript.DynamicRuntime;

namespace PlayScript
{
	// this struct can hold any playscript value 
	// this is used to prevent unnecessary boxing of value types (bool/int/number etc)
#if ALIGN32
	[StructLayout(LayoutKind.Explicit, Size=16)]
#else
	[StructLayout(LayoutKind.Explicit, Size=24)]
#endif
	[DebuggerDisplay("{Type} {IntValue} {NumberValue} {ObjectValue}")]
	public struct Variant : IEquatable<Variant>
	{
		// type code for variant
		public enum TypeCode
		{
			Undefined,
			Null,
			Boolean,
			Int,
			UInt,
			Number,
			String,
			Object
		};

// depending on alignment requirements and reference size we have to layout the structure differently
#if ALIGN32
		[FieldOffset(0)]
		private TypeCode 	mType;
		[FieldOffset(4)]
		private object 		mObject;	
		[FieldOffset(8)]
		private double 		mNumberValue;
		[FieldOffset(8)]
		private int 		mIntValue;
		[FieldOffset(8)]
		private bool 		mBoolValue;
#else
		[FieldOffset(0)]
		private TypeCode 	mType;
		[FieldOffset(8)]
		private object 		mObject;	
		[FieldOffset(16)]
		private double 		mNumberValue;
		[FieldOffset(16)]
		private int 		mIntValue;
		[FieldOffset(16)]
		private bool 		mBoolValue;
#endif

		// helper cast
		private uint   UIntValue   {get {return (uint)mIntValue;}}

		//
		// constructors for different types
		// unfortunately all fields must be initialized even if they overlap in a union
		//

		// undefined value
		public static Variant Undefined = new Variant(TypeCode.Undefined);

		// null value
		public static Variant Null = new Variant(TypeCode.Null);

		private Variant(TypeCode type)
		{
			mType = type;
			mObject = null;
			mBoolValue = false;
			mNumberValue = 0.0;
			mIntValue = 0;
		}

		public Variant(bool value)
		{
			mType = TypeCode.Boolean;
			mObject = null;
			mNumberValue = 0.0;
			mIntValue = 0;
			mBoolValue = value;
		}

		public Variant(int value)
		{
			mType = TypeCode.Int;
			mObject = null;
			mBoolValue = false;
			mNumberValue = 0.0;
			mIntValue = value;
		}

		public Variant(uint value)
		{
			mType = TypeCode.UInt;
			mObject = null;
			mBoolValue = false;
			mNumberValue = 0.0;
			mIntValue = (int)value;
		}

		public Variant(double value)
		{
			mType = TypeCode.Number;
			mObject = null;
			mBoolValue = false;
			mIntValue = 0;
			mNumberValue = value;
		}

		public Variant(string value)
		{
			mType = TypeCode.String;
			mObject = value;
			mBoolValue = false;
			mNumberValue = 0.0;
			mIntValue = 0;
		}

		public Variant(object value)
		{
			mType = TypeCode.Object;
			mObject = value;
			mBoolValue = false;
			mNumberValue = 0.0;
			mIntValue = 0;
		}

		public TypeCode Type
		{
			get {return mType;}
		}

		public bool IsDefined
		{
			get
			{
				return mType != TypeCode.Undefined;
			}
		}

		public bool IsNull
		{
			get
			{
				return mType == TypeCode.Null;
			}
		}

		public bool IsBoolean
		{
			get
			{
				return mType == TypeCode.Boolean;
			}
		}

		public bool IsString
		{
			get
			{
				return mType == TypeCode.String;
			}
		}

		public bool IsNumeric
		{
			get
			{
				return mType == TypeCode.Int || mType == TypeCode.UInt || mType == TypeCode.Number;
			}
		}

		// set to true if the value is the default (false,0,null)
		public bool HasDefaultValue
		{
			get 
			{
				switch (mType) {
				case TypeCode.Undefined:
					return true; 
				case TypeCode.Null:
					return true;
				case TypeCode.Boolean:
					return mBoolValue == false;
				case TypeCode.Int:
					return mIntValue == 0;
				case TypeCode.UInt:
					return UIntValue == 0;
				case TypeCode.Number:
					return mNumberValue == 0.0;
				case TypeCode.String:
					return mObject == null;
				case TypeCode.Object:
					return mObject == null;
				default:
					throw new InvalidCastException();
				}
			}
		}

		public override string ToString()
		{
			switch (mType) {
			case TypeCode.Undefined:
				return "<undefined>";
			case TypeCode.Null:
				return "<null>";
			case TypeCode.Boolean:
				return mBoolValue ? "true" : "false";
			case TypeCode.Int:
				return mIntValue.ToString();
			case TypeCode.UInt:
				return UIntValue.ToString();
			case TypeCode.Number:
				return mNumberValue.ToString();
			case TypeCode.String:
				return (string)mObject;
			case TypeCode.Object:
				return mObject.ToString();
			default:
				return "Type: " + this.mType.ToString();
			}
		}

		//
		// conversion operators (variant -> system types)
		//

		public dynamic AsDynamic()
		{
			return (dynamic)AsObject();
		}

		public object AsObject()
		{
			switch (mType) {
			case TypeCode.Undefined:
				return null;		// casting undefined to object == null
			case TypeCode.Null:
				return null;
			case TypeCode.Boolean:
				return mBoolValue ? sBoolTrue : sBoolFalse;
			case TypeCode.Int:
				if (mIntValue == 0) return sIntZero;
				if (mIntValue == 1) return sIntOne;
				if (mIntValue ==-1) return sIntNegOne;
				return (object)mIntValue;	// box integer
			case TypeCode.UInt:
				if (UIntValue == 0) return sUIntZero;
				if (UIntValue == 1) return sUIntOne;
				return (object)UIntValue;	// box integer
			case TypeCode.Number:
				if (mNumberValue == 0.0) return sNumberZero;
				if (mNumberValue == 1.0) return sNumberOne;
				return (object)mNumberValue; // box number
			case TypeCode.String:
				return mObject;			
			case TypeCode.Object:
				return mObject;
			default:
				throw new InvalidCastException();
			}
		}

		public int AsInt()
		{
			if (mType == TypeCode.Int) {
				return mIntValue;
			}
			if (mType == TypeCode.Number) {
				return (int)mNumberValue;
			}
			if (mType == TypeCode.UInt) {
				return (int)UIntValue;
			}
			if (mType == TypeCode.Undefined) {
				return 0;
			}
			if (mType == TypeCode.String) {
				string s =(string)mObject;
				if (s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)) {
					// Hex number - Use Convert.ToInt32() so we don't have to strip "0x" from the string.
					return Convert.ToInt32(s, 16);
				} else {
					return int.Parse(s);
				}
			}
			throw new InvalidCastException("Cannot cast to Int");
		}

		public uint AsUInt()
		{
			if (mType == TypeCode.UInt) {
				return UIntValue;
			}
			if (mType == TypeCode.Int) {
				return (uint)mIntValue;
			}
			if (mType == TypeCode.Number) {
				return (uint)mNumberValue;
			}
			if (mType == TypeCode.Undefined) {
				return 0;
			}
			if (mType == TypeCode.String) {
				string s =(string)mObject;
				if (s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)) {
					// Hex number - Use Convert.ToUInt32() so we don't have to strip "0x" from the string.
					return Convert.ToUInt32(s, 16);
				} else {
					return uint.Parse(s);
				}
			}
			throw new InvalidCastException("Cannot cast to UInt");
		}

		public bool AsBoolean()
		{
			if (mType == TypeCode.Boolean) {
				return mBoolValue;
			}
			if (mType == TypeCode.Null) {
				return false;
			}
			if (mType == TypeCode.Undefined) {
				return false;
			}
			if (mType == TypeCode.Int) {
				return mIntValue != 0;
			}
			if (mType == TypeCode.Number) {
				return mNumberValue != 0.0;
			}
			if (mType == TypeCode.UInt) {
				return UIntValue != 0;
			}
			throw new InvalidCastException("Cannot cast to Boolean");
		}

		public double AsNumber()
		{
			if (mType == TypeCode.Number) {
				return (double)mNumberValue;
			}
			if (mType == TypeCode.Int) {
				return (double)mIntValue;
			}
			if (mType == TypeCode.Undefined) {
				return 0.0;
			}
			if (mType == TypeCode.String) {
				return double.Parse((string)mObject);
			}
			if (mType == TypeCode.UInt) {
				return (double)UIntValue;
			}
			throw new InvalidCastException("Cannot cast to Number");
		}

		public float AsFloat()
		{
			if (mType == TypeCode.Number) {
				return (float)mNumberValue;
			}
			if (mType == TypeCode.Int) {
				return (float)mIntValue;
			}
			if (mType == TypeCode.Undefined) {
				return 0.0f;
			}
			if (mType == TypeCode.String) {
				return float.Parse((string)mObject);
			}
			if (mType == TypeCode.UInt) {
				return (float)UIntValue;
			}
			throw new InvalidCastException("Cannot cast to float");
		}

		public string AsString()
		{
			if (mType == TypeCode.String) {
				return (string)mObject;
			}
			switch (mType) {
			case TypeCode.Undefined:
			case TypeCode.Null:
				return null;
			case TypeCode.Boolean:
				return mBoolValue ? "true" : "false";
			case TypeCode.Int:
				return mIntValue.ToString();
			case TypeCode.UInt:
				return UIntValue.ToString();
			case TypeCode.Number:
				return mNumberValue.ToString();
			case TypeCode.Object:
				return mObject.ToString();
			default:
				throw new InvalidCastException("Cannot cast to String");
			}
		}

		public object AsType(System.Type type)
		{
			var typeCode = System.Type.GetTypeCode (type);
			switch (typeCode) {
			case System.TypeCode.Int32:
				return AsInt();
			case System.TypeCode.Double:
				return AsNumber();
			case System.TypeCode.Boolean:
				return AsBoolean();
			case System.TypeCode.UInt32:
				return AsUInt();
			case System.TypeCode.Single:
				return AsFloat();
			case System.TypeCode.String:
				return AsString();
			case System.TypeCode.Object:
				return AsObject();
			default:
				throw new InvalidCastException ("Invalid cast to type:" + type.ToString());
			}
		}

		// implicit conversions to variant
		public static implicit operator Variant(bool value)
		{
			return new Variant(value);
		}

		public static implicit operator Variant(int value)
		{
			return new Variant(value);
		}

		public static implicit operator Variant(uint value)
		{
			return new Variant(value);
		}

		public static implicit operator Variant(double value)
		{
			return new Variant(value);
		}

		public static implicit operator Variant(string value)
		{
			return new Variant(value);
		}

		// conversions from variant (should these be implicit or explicit?
		public static explicit operator bool(Variant variant)
		{
			return variant.AsBoolean();
		}
		public static explicit operator int(Variant variant)
		{
			return variant.AsInt();
		}
		public static explicit operator uint(Variant variant)
		{
			return variant.AsUInt();
		}
		public static explicit operator double(Variant variant)
		{
			return variant.AsNumber();
		}
		public static explicit operator string(Variant variant)
		{
			return variant.AsString();
		}

		// creates a variant from an object, examining the object's type appropriately
		public static Variant FromAnyType(object o)
		{
			if (o == null) {
				return new Variant(TypeCode.Null);
			}
			if (o == PlayScript.Undefined._undefined) {
				return new Variant(TypeCode.Undefined);
			}

			var typeCode = System.Type.GetTypeCode (o.GetType());
			switch (typeCode) {
			case System.TypeCode.Int32:
				return new Variant((int)o);
			case System.TypeCode.Single:
				return new Variant((double)(float)o);
			case System.TypeCode.Double:
				return new Variant((double)o);
			case System.TypeCode.Boolean:
				return new Variant((bool)o);
			case System.TypeCode.UInt32:
				return new Variant((uint)o);
			case System.TypeCode.String:
				return new Variant((string)o);
			case System.TypeCode.Object:
				return new Variant(o);
			default:
				throw new InvalidCastException ("Invalid cast to type:" + o.GetType().ToString());
			}
		}

		#region IEquatable implementation
		public bool Equals(Variant other)
		{
			if (this.mType != other.mType) {
				// compare numeric values by promoting them
				if (this.IsNumeric && other.IsNumeric) {
					return this.AsNumber() == other.AsNumber();
				}

				// TODO we should do some type conversion here
				return false;
			}

			// they are both the same type
			switch (mType) {
			case TypeCode.Undefined:
				return false;
			case TypeCode.Null:
				return true;
			case TypeCode.Boolean:
				return this.mBoolValue == other.mBoolValue;
			case TypeCode.Int:
				return this.mIntValue == other.mIntValue;
			case TypeCode.UInt:
				return this.UIntValue == other.UIntValue;
			case TypeCode.Number:
				return this.mNumberValue == other.mNumberValue;
			case TypeCode.String:
				return ((string)mObject) == ((string)other.mObject);
			case TypeCode.Object:
				return mObject.Equals(other.mObject);
			default:
				throw new InvalidCastException(mType.ToString());
			}

		}
		#endregion


		// pre-boxed values
		private static readonly object sBoolTrue = (object)true;
		private static readonly object sBoolFalse = (object)false;
		private static readonly object sIntNegOne = (object)(int)-1;
		private static readonly object sIntZero = (object)(int)0;
		private static readonly object sIntOne = (object)(int)1;
		private static readonly object sUIntZero = (object)(uint)0;
		private static readonly object sUIntOne = (object)(uint)1;
		private static readonly object sNumberZero = (object)(double)0.0;
		private static readonly object sNumberOne = (object)(double)1.0;

	};
}
