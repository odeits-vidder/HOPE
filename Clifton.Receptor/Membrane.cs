﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clifton.ExtensionMethods;

using Clifton.Receptor.Interfaces;
using Clifton.SemanticTypeSystem.Interfaces;

namespace Clifton.Receptor
{
	/// <summary>
	/// Membranes contain receptors and other membranes.
	/// </summary>
	public class Membrane : IMembrane
	{
		/// <summary>
		/// Fires when a new membrane is instantiated.
		/// </summary>
		public event EventHandler<MembraneEventArgs> NewMembrane;

		/// <summary>
		/// Fires when a new receptor is instantiated and registered into the system.
		/// </summary>
		public event EventHandler<ReceptorEventArgs> NewReceptor;

		/// <summary>
		/// Fires when a new carrier is created.
		/// </summary>
		public event EventHandler<NewCarrierEventArgs> NewCarrier;

		/// <summary>
		/// Fires when a receptor is removed.
		/// </summary>
		public event EventHandler<ReceptorEventArgs> ReceptorRemoved;

		public string Name { get; set; }
		public ISemanticTypeSystem SemanticTypeSystem { get; protected set; }
		public List<IMembrane> Membranes { get; protected set; }
		public Dictionary<PermeabilityKey, PermeabilityConfiguration> ProtocolPermeability {get; protected set;}

		// Return non-system receptors.
		public ReadOnlyCollection<IReceptor> Receptors { get { return receptorSystem.Receptors.Where(r => !r.Instance.IsHidden).ToList().AsReadOnly(); } }

		public IMembrane ParentMembrane { get; protected set; }

		public IReceptor this[string name] { get { return receptorSystem[name]; } }

		protected ReceptorsContainer receptorSystem;
		protected ISemanticTypeSystem semanticTypeSystem;

		public Membrane(ISemanticTypeSystem sts)
		{
			receptorSystem = new ReceptorsContainer(sts);
			SemanticTypeSystem = sts;

			receptorSystem.NewReceptor += OnNewReceptor;
			receptorSystem.NewCarrier += OnNewCarrier;
			receptorSystem.ReceptorRemoved += OnReceptorRemoved;

			Membranes = new List<IMembrane>();
			ProtocolPermeability = new Dictionary<PermeabilityKey, PermeabilityConfiguration>();
		}

		/// <summary>
		/// Resets the receptor system, clears the protocol permeability, recursively resets inner membranes, and dissociates all child receptors.
		/// </summary>
		public void Reset()
		{
			Membranes.ForEach(m => m.Reset());
			receptorSystem.Reset();
			Membranes.Clear();
			ProtocolPermeability.Clear();
		}

		/// <summary>
		/// Call on each receptor instance when the system has been fully initialized (applet or a receptor is dropped onto the surface.)
		/// </summary>
		public void EndSystemInit()
		{
			Membranes.ForEach(m => m.EndSystemInit());
			receptorSystem.EndSystemInit();
		}

		// TODO: The receptors container should acquire this list, rather than it being set.
		public void UpdateMasterConnectionList(Dictionary<IReceptor, List<IReceptor>> masterList)
		{
			receptorSystem.MasterReceptorConnectionList = masterList;
		}

		public void LoadReceptors(Action<IReceptor> afterRegister = null)
		{
			receptorSystem.LoadReceptors(afterRegister);
		}

		public ICarrier CreateCarrier(IReceptorInstance from, ISemanticTypeStruct protocol, dynamic signal)
		{
			return receptorSystem.CreateCarrier(from, protocol, signal);
		}

		public void CreateCarrierIfReceiver(IReceptorInstance from, ISemanticTypeStruct protocol, dynamic signal)
		{
			receptorSystem.CreateCarrierIfReceiver(from, protocol, signal);
		}

		public ICarrier CreateInternalCarrier(ISemanticTypeStruct protocol, dynamic signal)
		{
			return receptorSystem.CreateInternalCarrier(protocol, signal);
		}

		public IReceptor RegisterReceptor(string fn)
		{
			IReceptor receptor = receptorSystem.RegisterReceptor(fn);

			return receptor;
		}

		public IReceptor RegisterReceptor(string name, string assemblyName)
		{
			IReceptor receptor = receptorSystem.RegisterReceptor(name, assemblyName);

			return receptor;
		}

		public void RegisterReceptor(string name, IReceptorInstance inst)
		{
			receptorSystem.RegisterReceptor(name, inst);
		}

		public void Remove(IReceptor receptor)
		{
			receptorSystem.Remove(receptor);
		}

		public void Remove(IReceptorInstance receptorInstance)
		{
			receptorSystem.Remove(receptorInstance);
		}

		/// <summary>
		/// Remove this membrane, moving its receptors to the parent.
		/// </summary>
		public void Dissolve()
		{
			// TODO: To much conversion.
			MoveReceptorsToMembrane(Receptors.Cast<IReceptor>().ToList(), ParentMembrane);
			ParentMembrane.Membranes.Remove(this);
		}

		/// <summary>
		/// Recurse through the entire membrane tree to find the membranes containing the receptors in the list.
		/// </summary>
		public List<Membrane> GetMembranesContaining(List<IReceptor> receptorList)
		{
			List<Membrane> ret = new List<Membrane>();

			receptorList.ForEach(r => ret.Add(GetMembraneContaining(r)));

			// Return the distinct membranes.
			return ret.Distinct().ToList();
		}

		// TODO: Maintain a flat list to avoid this recursion.
		/// <summary>
		/// Recurse through membranes to find the membrane containing the desired receptor.
		/// </summary>
		public Membrane GetMembraneContaining(IReceptor searchFor)
		{
			Membrane ret = null;

			if (Receptors.Contains((Receptor)searchFor))
			{
				ret = this;
			}
			else
			{
				foreach (Membrane childMembrane in Membranes)
				{
					ret = childMembrane.GetMembraneContaining(searchFor);

					if (ret != null)
					{
						break;
					}
				}
			}

			return ret;
		}

		/// <summary>
		/// Moves receptors in this membrane to the target membrane.
		/// </summary>
		public void MoveReceptorsToMembrane(List<IReceptor> receptors, IMembrane targetMembrane)
		{
			receptors.ForEach(r=>MoveReceptorToMembrane(r, targetMembrane));
		}

		/// <summary>
		/// Moves the receptor in this membrane to the specified target membrane.
		/// </summary>
		public void MoveReceptorToMembrane(IReceptor receptor, IMembrane targetMembrane)
		{
			receptorSystem.MoveReceptorTo(receptor, ((Membrane)targetMembrane).receptorSystem);
			UpdatePermeability();
			((Membrane)targetMembrane).UpdatePermeability();
		}

		public Membrane CreateInnerMembrane()
		{
			Membrane inner = new Membrane(SemanticTypeSystem);
			Membranes.Add(inner);
			inner.ParentMembrane = this;
			NewMembrane.Fire(this, new MembraneEventArgs(inner));

			return inner;
		}

		/// <summary>
		/// Returns the list of distinct protocols that each receptor instance in the membrane emits.
		/// </summary>
		public List<string> GetEmittedProtocols()
		{
			List<string> ret = new List<string>();

			Receptors.ForEach(r => ret.AddRange(r.Instance.GetEmittedProtocols()));

			return ret.Distinct().ToList();
		}

		/// <summary>
		///  Returns the list of distinct protocols that each receptor isntance in the membrane can receive.
		/// </summary>
		/// <returns></returns>
		public List<string> GetListeningProtocols()
		{
			List<string> ret = new List<string>();

			Receptors.ForEach(r => ret.AddRange(r.Instance.GetReceiveProtocols().Select(rp=>rp.Protocol)));

			return ret.Distinct().ToList();
		}

		public void ProcessQueuedCarriers()
		{
			receptorSystem.ProcessQueuedCarriers();
		}

		protected void OnNewReceptor(object sender, ReceptorEventArgs args)
		{
			// Forward to other listeners.
			NewReceptor.Fire(this, args);
			UpdatePermeability();
		}

		protected void OnNewCarrier(object sender, NewCarrierEventArgs args)
		{
			// Forward to other listeners.
			NewCarrier.Fire(this, args);
		}

		protected void OnReceptorRemoved(object sender, ReceptorEventArgs args)
		{
			// Forward to other listeners.
			ReceptorRemoved.Fire(this, args);
			UpdatePermeability();
		}

		/// <summary>
		/// Updates (creating new, deleting removed) protocol permeability.
		/// Remember, we only care if the membrane is permeable to the protocol, regardless of
		/// the number and types of receptors on either side of the membrane.
		/// However, the ProtocolPermeability "key" is composite (name and direction) because
		/// emitters (and listeners) of the same protocol can live on both sides of a membrane.
		/// </summary>
		protected void UpdatePermeability()
		{
			List<PermeabilityKey> allKeys = ProtocolPermeability.Keys.ToList();

			// Create new entries for new emitted protocols:
			List<string> emitted = GetEmittedProtocols();

			emitted.ForEach(p =>
				{
					PermeabilityKey pk = new PermeabilityKey() {Protocol = p, Direction = PermeabilityDirection.Out};

					if (!ProtocolPermeability.ContainsKey(pk))
					{
						ProtocolPermeability[pk] = new PermeabilityConfiguration();
					}
					else
					{
						// Entry is in use.
						allKeys.Remove(pk);
					}
				});

			// Create new entries for new receiving protocols:
			List<string> listening = GetListeningProtocols();

			listening.ForEach(p=>
				{
					PermeabilityKey pk = new PermeabilityKey() { Protocol = p, Direction = PermeabilityDirection.In };

					if (!ProtocolPermeability.ContainsKey(pk))
					{
						ProtocolPermeability[pk] = new PermeabilityConfiguration();
					}
					else
					{
						// Entry is in use.
						allKeys.Remove(pk);
					}
				});

			// Remove entries that no longer are in use.
			allKeys.ForEach(p => ProtocolPermeability.Remove(p));
		}
	}
}
