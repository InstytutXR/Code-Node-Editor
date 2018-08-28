﻿using System;

namespace NodeEditor
{
	public interface ISlot : IEquatable<ISlot>
	{
		int id { get; }
		string displayName { get; set; }
		bool isInputSlot { get; }
		bool isOutputSlot { get; }
		SlotReference slotReference { get; }
		INode owner { get; set; }
		bool hidden { get; set; }
		bool allowMultipleConnections { get; }
	}
}