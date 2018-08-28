﻿using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor.Nodes;
using NodeEditor.Util;
using UnityEngine;

namespace NodeEditor
{
	public abstract class AbstractNodeGraph : IGraph, ISerializationCallbackReceiver, IDisposable
	{
		private static List<IEdge> m_EmptyEdgeList = new List<IEdge>();

		public IGraphObject owner { get; set; }

		public object context { get; set; }

		public event Action<INode> onNodeAdded;

		#region Property data
		[NonSerialized]
		List<INodeProperty> m_Properties = new List<INodeProperty>();

		[NonSerialized]
		Dictionary<Guid,INodeProperty> m_PropertyDictionary = new Dictionary<Guid, INodeProperty>();

		public IReadOnlyList<INodeProperty> GetProperties()
		{
			return m_Properties;
		}

		public INodeProperty GetProperty(Guid id)
		{
			INodeProperty prop;
			if (m_PropertyDictionary.TryGetValue(id, out prop))
			{
				return prop;
			}
			return null;
		}

		public IEnumerable<INodeProperty> properties => m_Properties;

		[SerializeField]
		List<SerializationHelper.JSONSerializedElement> m_SerializedProperties = new List<SerializationHelper.JSONSerializedElement>();

		[NonSerialized]
		List<INodeProperty> m_AddedProperties = new List<INodeProperty>();

		public IEnumerable<INodeProperty> addedProperties => m_AddedProperties;

		[NonSerialized]
		List<Guid> m_RemovedProperties = new List<Guid>();

		public IEnumerable<Guid> removedProperties => m_RemovedProperties;

		[NonSerialized]
		List<INodeProperty> m_MovedProperties = new List<INodeProperty>();

		public IEnumerable<INodeProperty> movedProperties => m_MovedProperties;

		[SerializeField]
		SerializableGuid m_GUID = new SerializableGuid();

		public Guid guid => m_GUID.guid;

		#endregion

		#region Node data

		[NonSerialized]
		Stack<Identifier> m_FreeNodeTempIds = new Stack<Identifier>();

		[NonSerialized]
		List<INode> m_Nodes = new List<INode>();

		[NonSerialized]
		Dictionary<Guid, INode> m_NodeDictionary = new Dictionary<Guid, INode>();

		public IEnumerable<T> GetNodes<T>() where T : INode
		{
			return m_Nodes.Where(x => x != null).OfType<T>();
		}

		public IReadOnlyList<INode> GetNodes()
		{
			return m_Nodes;
		}

		[SerializeField]
		List<SerializationHelper.JSONSerializedElement> m_SerializableNodes = new List<SerializationHelper.JSONSerializedElement>();

		[NonSerialized]
		List<INode> m_AddedNodes = new List<INode>();

		public IEnumerable<INode> addedNodes => m_AddedNodes;

		[NonSerialized]
		List<INode> m_RemovedNodes = new List<INode>();

		public IEnumerable<INode> removedNodes => m_RemovedNodes;

		[NonSerialized]
		List<INode> m_PastedNodes = new List<INode>();

		public IEnumerable<INode> pastedNodes => m_PastedNodes;

		#endregion

		#region Edge data

		[NonSerialized]
		List<IEdge> m_Edges = new List<IEdge>();

		public IReadOnlyList<IEdge> GetEdges()
		{
			return m_Edges;
		}

		public IReadOnlyList<IEdge> GetEdges(Guid nodeId)
		{
			List<IEdge> edges;
			if (m_NodeEdges.TryGetValue(nodeId, out edges))
			{
				return edges;
			}
			return m_EmptyEdgeList;
		}

		[SerializeField]
		List<SerializationHelper.JSONSerializedElement> m_SerializableEdges = new List<SerializationHelper.JSONSerializedElement>();

		[NonSerialized]
		Dictionary<Guid, List<IEdge>> m_NodeEdges = new Dictionary<Guid, List<IEdge>>();

		[NonSerialized]
		List<IEdge> m_AddedEdges = new List<IEdge>();

		public IEnumerable<IEdge> addedEdges => m_AddedEdges;

		[NonSerialized]
		List<IEdge> m_RemovedEdges = new List<IEdge>();

		public IEnumerable<IEdge> removedEdges => m_RemovedEdges;

		#endregion

		private bool m_Initialized;

		public string name { get; set; }

		[SerializeField]
		string m_Path;

		public string path
		{
			get { return m_Path; }
			set
			{
				if (m_Path == value)
					return;
				m_Path = value;
				owner.RegisterCompleteObjectUndo("Change Path");
			}
		}

		public void ClearChanges()
		{
			m_AddedNodes.Clear();
			m_RemovedNodes.Clear();
			m_PastedNodes.Clear();
			m_AddedEdges.Clear();
			m_RemovedEdges.Clear();
			m_AddedProperties.Clear();
			m_RemovedProperties.Clear();
			m_MovedProperties.Clear();
		}

		public virtual void AddNode(INode node)
		{
			if (node is AbstractNode)
			{
				AddNodeNoValidate(node);
				ValidateGraph();
			}
			else
			{
				Debug.LogWarningFormat("Trying to add node {0} to Material graph, but it is not a {1}", node, typeof(AbstractNode));
			}
		}

		void AddNodeNoValidate(INode node)
		{
			node.SetOwner(this);
			if (m_FreeNodeTempIds.Any())
			{
				var id = m_FreeNodeTempIds.Pop();
				id.IncrementVersion();
				node.tempId = id;
				m_Nodes[id.index] = node;
			}
			else
			{
				var id = new Identifier(m_Nodes.Count);
				node.tempId = id;
				m_Nodes.Add(node);
			}
			m_NodeDictionary.Add(node.guid, node);
			m_AddedNodes.Add(node);
			var abstractNode = node as AbstractNode;
			abstractNode?.OnAdd();
			onNodeAdded?.Invoke(node);
		}

		public void RemoveNode(INode node)
		{
			if (!node.canDeleteNode)
				return;
			RemoveNodeNoValidate(node);
			ValidateGraph();
		}

		void RemoveNodeNoValidate(INode node)
		{
			var abstractNode = (AbstractNode)node;
			if (!abstractNode.canDeleteNode)
				return;

			abstractNode.Dispose();
			abstractNode.OnRemove();
			m_Nodes[abstractNode.tempId.index] = null;
			m_FreeNodeTempIds.Push(abstractNode.tempId);
			m_NodeDictionary.Remove(abstractNode.guid);
			m_RemovedNodes.Add(abstractNode);
		}

		void AddEdgeToNodeEdges(IEdge edge)
		{
			List<IEdge> inputEdges;
			if (!m_NodeEdges.TryGetValue(edge.inputSlot.nodeGuid, out inputEdges))
				m_NodeEdges[edge.inputSlot.nodeGuid] = inputEdges = new List<IEdge>();
			inputEdges.Add(edge);

			List<IEdge> outputEdges;
			if (!m_NodeEdges.TryGetValue(edge.outputSlot.nodeGuid, out outputEdges))
				m_NodeEdges[edge.outputSlot.nodeGuid] = outputEdges = new List<IEdge>();
			outputEdges.Add(edge);
		}

		IEdge ConnectNoValidate(SlotReference fromSlotRef, SlotReference toSlotRef)
		{
			var fromNode = GetNodeFromGuid(fromSlotRef.nodeGuid);
			var toNode = GetNodeFromGuid(toSlotRef.nodeGuid);

			if (fromNode == null || toNode == null)
				return null;

			// if fromNode is already connected to toNode
			// do now allow a connection as toNode will then
			// have an edge to fromNode creating a cycle.
			// if this is parsed it will lead to an infinite loop.
			var dependentNodes = new List<INode>();
			NodeUtils.CollectNodesNodeFeedsInto(dependentNodes, toNode);
			if (dependentNodes.Contains(fromNode))
				return null;

			var fromSlot = fromNode.FindSlot<ISlot>(fromSlotRef.slotId);
			var toSlot = toNode.FindSlot<ISlot>(toSlotRef.slotId);

			if (fromSlot.isOutputSlot == toSlot.isOutputSlot)
				return null;

			var inputSlot = fromSlot.isInputSlot ? fromSlot : toSlot;
			var outputSlotRef = fromSlot.isOutputSlot ? fromSlotRef : toSlotRef;
			var inputSlotRef = fromSlot.isInputSlot ? fromSlotRef : toSlotRef;

			if (!inputSlot.allowMultipleConnections)
			{
				// remove any inputs that exits before adding if only a single connection is allowed
				foreach (var edge in this.GetEdges(inputSlotRef))
				{
					RemoveEdgeNoValidate(edge);
				}
			}

			var newEdge = new Edge(outputSlotRef, inputSlotRef);
			m_Edges.Add(newEdge);
			m_AddedEdges.Add(newEdge);
			AddEdgeToNodeEdges(newEdge);

			//Debug.LogFormat("Connected edge: {0} -> {1} ({2} -> {3})\n{4}", newEdge.outputSlot.nodeGuid, newEdge.inputSlot.nodeGuid, fromNode.name, toNode.name, Environment.StackTrace);
			return newEdge;
		}

		public virtual IEdge Connect(SlotReference fromSlotRef, SlotReference toSlotRef)
		{
			var newEdge = ConnectNoValidate(fromSlotRef, toSlotRef);
			ValidateGraph();
			return newEdge;
		}

		public virtual void RemoveEdge(IEdge e)
		{
			RemoveEdgeNoValidate(e);
			ValidateGraph();
		}

		public void RemoveElements(IEnumerable<INode> nodes, IEnumerable<IEdge> edges)
		{
			foreach (var edge in edges.ToArray())
				RemoveEdgeNoValidate(edge);

			foreach (var serializableNode in nodes.ToArray())
				RemoveNodeNoValidate(serializableNode);

			ValidateGraph();
		}

		protected void RemoveEdgeNoValidate(IEdge e)
		{
			e = m_Edges.FirstOrDefault(x => x.Equals(e));
			//if (e == null)
				//throw new ArgumentException("Trying to remove an edge that does not exist.", "e");
			m_Edges.Remove(e);

			List<IEdge> inputNodeEdges;
			if (m_NodeEdges.TryGetValue(e.inputSlot.nodeGuid, out inputNodeEdges))
				inputNodeEdges.Remove(e);

			List<IEdge> outputNodeEdges;
			if (m_NodeEdges.TryGetValue(e.outputSlot.nodeGuid, out outputNodeEdges))
				outputNodeEdges.Remove(e);

			m_RemovedEdges.Add(e);
		}

		public INode GetNodeFromGuid(Guid guid)
		{
			INode node;
			m_NodeDictionary.TryGetValue(guid, out node);
			return node;
		}

		public INode GetNodeFromTempId(Identifier tempId)
		{
			if (tempId.index > m_Nodes.Count)
				throw new ArgumentException("Trying to retrieve a node using an identifier that does not exist.");
			var node = m_Nodes[tempId.index];
			if (node == null)
				throw new Exception("Trying to retrieve a node using an identifier that does not exist.");
			if (node.tempId.version != tempId.version)
				throw new Exception("Trying to retrieve a node that was removed from the graph.");
			return node;
		}

		public bool ContainsNodeGuid(Guid guid)
		{
			return m_NodeDictionary.ContainsKey(guid);
		}

		public T GetNodeFromGuid<T>(Guid guid) where T : INode
		{
			var node = GetNodeFromGuid(guid);
			if (node is T)
				return (T)node;
			return default(T);
		}

		public void AddShaderProperty(INodeProperty property)
		{
			if (property == null)
				return;

			if (m_Properties.Contains(property))
				return;

			m_Properties.Add(property);
			m_PropertyDictionary.Add(property.guid,property);
			m_AddedProperties.Add(property);
		}

		public string SanitizePropertyName(string displayName, Guid guid = default(Guid))
		{
			displayName = displayName.Trim();
			return GraphUtil.SanitizeName(m_Properties.Where(p => p.guid != guid).Select(p => p.displayName), "{0} ({1})", displayName);
		}

		public void RemoveShaderProperty(Guid guid)
		{
			var propertyNodes = GetNodes<PropertyNode>().Where(x => x.propertyGuid == guid).ToList();
			foreach (var propNode in propertyNodes)
				ReplacePropertyNodeWithConcreteNodeNoValidate(propNode);

			RemoveShaderPropertyNoValidate(guid);

			ValidateGraph();
		}

		public void MoveShaderProperty(INodeProperty property, int newIndex)
		{
			if (newIndex > m_Properties.Count || newIndex < 0)
				throw new ArgumentException("New index is not within properties list.");
			var currentIndex = m_Properties.IndexOf(property);
			if (currentIndex == -1)
				throw new ArgumentException("Property is not in graph.");
			if (newIndex == currentIndex)
				return;
			m_Properties.RemoveAt(currentIndex);
			if (newIndex > currentIndex)
				newIndex--;
			var isLast = newIndex == m_Properties.Count;
			if (isLast)
				m_Properties.Add(property);
			else
				m_Properties.Insert(newIndex, property);
			if (!m_MovedProperties.Contains(property))
				m_MovedProperties.Add(property);
		}

		public int GetShaderPropertyIndex(INodeProperty property)
		{
			return m_Properties.IndexOf(property);
		}

		void RemoveShaderPropertyNoValidate(Guid guid)
		{
			m_PropertyDictionary.Remove(guid);
			if (m_Properties.RemoveAll(x => x.guid == guid) > 0)
			{
				m_RemovedProperties.Add(guid);
				m_AddedProperties.RemoveAll(x => x.guid == guid);
				m_MovedProperties.RemoveAll(x => x.guid == guid);
			}
		}

		public void ReplacePropertyNodeWithConcreteNode(PropertyNode propertyNode)
		{
			ReplacePropertyNodeWithConcreteNodeNoValidate(propertyNode);
			ValidateGraph();
		}

		void ReplacePropertyNodeWithConcreteNodeNoValidate(PropertyNode propertyNode)
		{
			var property = properties.FirstOrDefault(x => x.guid == propertyNode.propertyGuid);
			if (property == null)
				return;

			var node = property.ToConcreteNode();
			if (!(node is AbstractNode))
				return;

			var slot = propertyNode.FindOutputSlot<NodeSlot>(PropertyNode.OutputSlotId);
			var newSlot = node.GetOutputSlots<NodeSlot>().FirstOrDefault(s => s.valueType == slot.valueType);
			if (newSlot == null)
				return;

			node.drawState = propertyNode.drawState;
			AddNodeNoValidate(node);

			foreach (var edge in this.GetEdges(slot.slotReference))
				ConnectNoValidate(newSlot.slotReference, edge.inputSlot);

			RemoveNodeNoValidate(propertyNode);
		}

		public void ValidateGraph()
		{
			var propertyNodes = GetNodes<PropertyNode>().Where(n => !m_Properties.Any(p => p.guid == n.propertyGuid)).ToArray();
			foreach (var pNode in propertyNodes)
				ReplacePropertyNodeWithConcreteNodeNoValidate(pNode);

			//First validate edges, remove any
			//orphans. This can happen if a user
			//manually modifies serialized data
			//of if they delete a node in the inspector
			//debug view.
			foreach (var edge in m_Edges.ToArray())
			{
				var outputNode = GetNodeFromGuid(edge.outputSlot.nodeGuid);
				var inputNode = GetNodeFromGuid(edge.inputSlot.nodeGuid);

				NodeSlot outputSlot = null;
				NodeSlot inputSlot = null;
				if (outputNode != null && inputNode != null)
				{
					outputSlot = outputNode.FindOutputSlot<NodeSlot>(edge.outputSlot.slotId);
					inputSlot = inputNode.FindInputSlot<NodeSlot>(edge.inputSlot.slotId);
				}

				if (outputNode == null
				    || inputNode == null
				    || outputSlot == null
				    || inputSlot == null
				    || !outputSlot.IsCompatibleWith(inputSlot))
				{
					//orphaned edge
					RemoveEdgeNoValidate(edge);
				}
			}

			foreach (var node in GetNodes<INode>())
				node.ValidateNode();

			foreach (var edge in m_AddedEdges.ToList())
			{
				if (!ContainsNodeGuid(edge.outputSlot.nodeGuid) || !ContainsNodeGuid(edge.inputSlot.nodeGuid))
				{
					Debug.LogWarningFormat("Added edge is invalid: {0} -> {1}\n{2}", edge.outputSlot.nodeGuid, edge.inputSlot.nodeGuid, Environment.StackTrace);
					m_AddedEdges.Remove(edge);
				}
			}
		}

		public void ReplaceWith(IGraph other)
		{
			var otherMg = other as AbstractNodeGraph;
			if (otherMg == null)
				throw new ArgumentException("Can only replace with another AbstractNodeGraph", "other");

			using (var removedPropertiesPooledObject = ListPool<Guid>.GetDisposable())
			{
				var removedPropertyGuids = removedPropertiesPooledObject.value;
				foreach (var property in m_Properties)
					removedPropertyGuids.Add(property.guid);
				foreach (var propertyGuid in removedPropertyGuids)
					RemoveShaderPropertyNoValidate(propertyGuid);
			}
			foreach (var otherProperty in otherMg.properties)
			{
				if (!properties.Any(p => p.guid == otherProperty.guid))
					AddShaderProperty(otherProperty);
			}

			other.ValidateGraph();
			ValidateGraph();

			// Current tactic is to remove all nodes and edges and then re-add them, such that depending systems
			// will re-initialize with new references.
			using (var pooledList = ListPool<IEdge>.GetDisposable())
			{
				var removedNodeEdges = pooledList.value;
				removedNodeEdges.AddRange(m_Edges);
				foreach (var edge in removedNodeEdges)
					RemoveEdgeNoValidate(edge);
			}

			using (var removedNodesPooledObject = ListPool<Guid>.GetDisposable())
			{
				var removedNodeGuids = removedNodesPooledObject.value;
				removedNodeGuids.AddRange(m_Nodes.Where(n => n != null).Select(n => n.guid));
				foreach (var nodeGuid in removedNodeGuids)
					RemoveNodeNoValidate(m_NodeDictionary[nodeGuid]);
			}

			ValidateGraph();

			foreach (var node in other.GetNodes())
				AddNodeNoValidate(node);

			foreach (var edge in other.GetEdges())
				ConnectNoValidate(edge.outputSlot, edge.inputSlot);

			ValidateGraph();
		}

		public void PasteGraph(CopyPasteGraph graphToPaste, List<INode> remappedNodes, List<IEdge> remappedEdges)
		{
			var nodeGuidMap = new Dictionary<Guid, Guid>();
			foreach (var node in graphToPaste.GetNodes<INode>())
			{
				INode pastedNode = node;

				var oldGuid = node.guid;
				var newGuid = node.RewriteGuid();
				nodeGuidMap[oldGuid] = newGuid;

				// Check if the property nodes need to be made into a concrete node.
				if (node is PropertyNode)
				{
					PropertyNode propertyNode = (PropertyNode)node;

					// If the property is not in the current graph, do check if the
					// property can be made into a concrete node.
					if (!m_Properties.Select(x => x.guid).Contains(propertyNode.propertyGuid))
					{
						// If the property is in the serialized paste graph, make the property node into a property node.
						var pastedGraphMetaProperties = graphToPaste.metaProperties.Where(x => x.guid == propertyNode.propertyGuid);
						if (pastedGraphMetaProperties.Any())
						{
							pastedNode = pastedGraphMetaProperties.FirstOrDefault().ToConcreteNode();
							pastedNode.drawState = node.drawState;
							nodeGuidMap[oldGuid] = pastedNode.guid;
						}
					}
				}

				var drawState = node.drawState;
				var position = drawState.position;
				position.x += 30;
				position.y += 30;
				drawState.position = position;
				node.drawState = drawState;
				remappedNodes.Add(pastedNode);
				AddNode(pastedNode);

				// add the node to the pasted node list
				m_PastedNodes.Add(pastedNode);
			}

			// only connect edges within pasted elements, discard
			// external edges.
			foreach (var edge in graphToPaste.edges)
			{
				var outputSlot = edge.outputSlot;
				var inputSlot = edge.inputSlot;

				Guid remappedOutputNodeGuid;
				Guid remappedInputNodeGuid;
				if (nodeGuidMap.TryGetValue(outputSlot.nodeGuid, out remappedOutputNodeGuid)
				    && nodeGuidMap.TryGetValue(inputSlot.nodeGuid, out remappedInputNodeGuid))
				{
					var outputSlotRef = new SlotReference(remappedOutputNodeGuid, outputSlot.slotId);
					var inputSlotRef = new SlotReference(remappedInputNodeGuid, inputSlot.slotId);
					remappedEdges.Add(Connect(outputSlotRef, inputSlotRef));
				}
			}

			ValidateGraph();
		}

		public void OnBeforeSerialize()
		{
			m_SerializableNodes = SerializationHelper.Serialize(GetNodes<INode>());
			m_SerializableEdges = SerializationHelper.Serialize<IEdge>(m_Edges);
			m_SerializedProperties = SerializationHelper.Serialize<INodeProperty>(m_Properties);
		}

		public virtual void OnAfterDeserialize()
		{
			// have to deserialize 'globals' before nodes
			m_Properties = SerializationHelper.Deserialize<INodeProperty>(m_SerializedProperties, GraphUtil.GetLegacyTypeRemapping());
			foreach (var property in m_Properties)
			{
				m_PropertyDictionary.Add(property.guid,property);
			}
			var nodes = SerializationHelper.Deserialize<INode>(m_SerializableNodes, GraphUtil.GetLegacyTypeRemapping());
			m_Nodes = new List<INode>(nodes.Count);
			m_NodeDictionary = new Dictionary<Guid, INode>(nodes.Count);
			foreach (var node in nodes.OfType<AbstractNode>())
			{
				node.SetOwner(this);
				node.UpdateNodeAfterDeserialization();
				node.tempId = new Identifier(m_Nodes.Count);
				m_Nodes.Add(node);
				m_NodeDictionary.Add(node.guid, node);
			}

			m_SerializableNodes = null;

			m_Edges = SerializationHelper.Deserialize<IEdge>(m_SerializableEdges, GraphUtil.GetLegacyTypeRemapping());
			m_SerializableEdges = null;
			foreach (var edge in m_Edges)
				AddEdgeToNodeEdges(edge);
		}

		public void OnEnable()
		{
			foreach (var node in GetNodes<INode>().OfType<IOnAssetEnabled>())
			{
				node.OnEnable();
			}
		}

		public void Dispose()
		{
			foreach (var disposable in GetNodes<INode>().OfType<IDisposable>())
			{
				disposable.Dispose();
			}
		}
	}
}