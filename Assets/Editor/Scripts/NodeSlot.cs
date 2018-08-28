﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace NodeEditor
{
	[Serializable]
	public abstract class NodeSlot : ISlot
	{
		const string k_NotInit = "Not Initilaized";

		[SerializeField] int m_Id;

		[NonSerialized] string m_DisplayName = k_NotInit;

		[SerializeField] SlotType m_SlotType = SlotType.Input;

		[NonSerialized] bool m_AllowMultipleConnections = false;

		[SerializeField] bool m_Hidden;

		bool m_HasError;

		public virtual VisualElement InstantiateControl()
		{
			return null;
		}

		public NodeSlot SetDisplayName(string name)
		{
			m_DisplayName = name;
			return this;
		}

		public virtual string displayName
		{
			get { return m_DisplayName; }
			set { m_DisplayName = value; }
		}

		public string RawDisplayName()
		{
			return m_DisplayName;
		}

		public SlotReference slotReference => new SlotReference(owner.guid, m_Id);

		public INode owner { get; set; }

		public NodeSlot SetAllowMultipleConnections(bool allow)
		{
			m_AllowMultipleConnections = allow;
			return this;
		}

		public bool allowMultipleConnections
		{
			get { return m_AllowMultipleConnections; }
			set { m_AllowMultipleConnections = value; }
		}

		public bool hidden
		{
			get { return m_Hidden; }
			set { m_Hidden = value; }
		}

		public int id
		{
			get { return m_Id; }
			protected internal set { m_Id = value; }
		}

		public bool isInputSlot => m_SlotType == SlotType.Input;

		public bool isOutputSlot => m_SlotType == SlotType.Output;

		public SlotType slotType
		{
			get { return m_SlotType; }
			protected internal set { m_SlotType = value; }
		}

		public bool isConnected
		{
			get
			{
				// node and graph respectivly
				if (owner == null || owner.owner == null)
					return false;

				var graph = owner.owner;
				var edges = graph.GetEdges(slotReference);
				return edges.Any();
			}
		}

		public bool hasError
		{
			get { return m_HasError; }
			set { m_HasError = value; }
		}


		public abstract SerializedType valueType
		{
			get;
		}

		public bool IsCompatibleWith(NodeSlot otherSlot)
		{
			return otherSlot != null
			       && otherSlot.owner != owner
			       && otherSlot.isInputSlot != isInputSlot
			       && otherSlot.isOutputSlot != isOutputSlot
			       && (otherSlot.valueType == valueType || otherSlot.valueType.Type.IsAssignableFrom(valueType));
		}

		public virtual void GetPreviewProperties(List<PreviewProperty> properties)
		{
			properties.Add(default(PreviewProperty));
		}

		public abstract void CopyValuesFrom(NodeSlot foundSlot);

		bool Equals(NodeSlot other)
		{
			return m_Id == other.m_Id && owner.guid.Equals(other.owner.guid);
		}

		public bool Equals(ISlot other)
		{
			return Equals(other as object);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NodeSlot) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (m_Id * 397) ^ (owner != null ? owner.GetHashCode() : 0);
			}
		}
	}
}