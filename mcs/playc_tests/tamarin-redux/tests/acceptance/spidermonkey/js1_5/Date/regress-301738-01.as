/* -*- Mode: C++; tab-width: 2; indent-tabs-mode: nil; c-basic-offset: 2 -*- */
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */
import com.adobe.test.Assert;


var gTestfile = 'regress-301738-01.js';
//-----------------------------------------------------------------------------
var BUGNUMBER = 301738;
var summary = 'Date parse compatibilty with MSIE';
var actual = '';
var expect = '';

//printBugNumber(BUGNUMBER);
//printStatus (summary);

/*
    Case 1. The input string contains an English month name.
    The form of the string can be month f l, or f month l, or
    f l month which each evaluate to the same date.
    If f and l are both greater than or equal to 70, or
    both less than 70, the date is invalid.
    The year is taken to be the greater of the values f, l.
    If the year is greater than or equal to 70 and less than 100,
    it is considered to be the number of years after 1900.
*/

var month = 'Jan';
var f;
var l;

f = l = 0;
expect = true;

actual = isNaN(new Date(month + ' ' + f + ' ' + l));
Assert.expectEq('January 0 0 is invalid', expect, actual);

actual = isNaN(new Date(f + ' ' + l + ' ' + month));
Assert.expectEq('0 0 January is invalid', expect, actual);

actual = isNaN(new Date(f + ' ' + month + ' ' + l));
Assert.expectEq('0 January 0 is invalid', expect, actual);

f = l = 70;

actual = isNaN(new Date(month + ' ' + f + ' ' + l));
Assert.expectEq('January 70 70 is invalid', expect, actual);

actual = isNaN(new Date(f + ' ' + l + ' ' + month));
Assert.expectEq('70 70 January is invalid', expect, actual);

actual = isNaN(new Date(f + ' ' + month + ' ' + l));
Assert.expectEq('70 January 70 is invalid', expect, actual);

f = 100;
l = 15;

// year, month, day
expect = new Date(f, 0, l).toString();

actual = new Date(month + ' ' + f + ' ' + l).toString();
Assert.expectEq('month f l', expect, actual);

actual = new Date(f + ' ' + l + ' ' + month).toString();
Assert.expectEq('f l month', expect, actual);

actual = new Date(f + ' ' + month + ' ' + l).toString();
Assert.expectEq('f month l', expect, actual);

f = 80;
l = 15;

// year, month, day
expect = (new Date(f, 0, l)).toString();

actual = (new Date(month + ' ' + f + ' ' + l)).toString();
Assert.expectEq('month f l', expect, actual);

actual = (new Date(f + ' ' + l + ' ' + month)).toString();
Assert.expectEq('f l month', expect, actual);

actual = (new Date(f + ' ' + month + ' ' + l)).toString();
Assert.expectEq('f month l', expect, actual);

f = 2040;
l = 15;

// year, month, day
expect = (new Date(f, 0, l)).toString();

actual = (new Date(month + ' ' + f + ' ' + l)).toString();
Assert.expectEq('month f l', expect, actual);

actual = (new Date(f + ' ' + l + ' ' + month)).toString();
Assert.expectEq('f l month', expect, actual);

actual = (new Date(f + ' ' + month + ' ' + l)).toString();
Assert.expectEq('f month l', expect, actual);


