﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace BitcoinCore
{
	/// <summary>
	/// A IHDScriptPubKey represent an object which represent a tree of scriptPubKeys
	/// </summary>
	public interface IHDScriptPubKey
	{
		IHDScriptPubKey? Derive(KeyPath keyPath);
		Script ScriptPubKey { get; }
	}
}
