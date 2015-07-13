/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */
import com.adobe.test.Assert;

// var SECTION = "Definitions\const";                  // provide a document reference (ie, ECMA section)
// var VERSION = "ActionScript 3.0";           // Version of JavaScript or ECMA
// var TITLE   = "conditional initialization of const globally";       // Provide ECMA section title or a description
var BUGNUMBER = "";

var cond:Boolean = true;

const num1:Number = (cond)? 3 : 0;


Assert.expectEq("Conditional initiailization of const", 3, num1);

