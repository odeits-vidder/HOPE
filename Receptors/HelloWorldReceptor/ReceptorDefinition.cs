﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Clifton.Receptor.Interfaces;
using Clifton.SemanticTypeSystem.Interfaces;

namespace HelloWorldReceptor
{
    public class ReceptorDefinition : BaseReceptor
    {
		public override string Name { get { return "Heartbeat"; } }

		protected Timer timer;

		public ReceptorDefinition(IReceptorSystem rsys) : base(rsys)
		{
			InitializeRepeatedHelloEvent();
			AddEmitProtocol("DebugMessage");
		}

		public override void Terminate()
		{
			timer.Stop();
			timer.Dispose();
		}

		protected void InitializeRepeatedHelloEvent()
		{
			timer = new Timer();
			timer.Interval = 1000 * 2;		// every 2 seconds.
			timer.Tick += SayHello;
			timer.Start();
		}

		protected void SayHello(object sender, EventArgs args)
		{
			ISemanticTypeStruct protocol = rsys.SemanticTypeSystem.GetSemanticTypeStruct("DebugMessage");
			dynamic signal = rsys.SemanticTypeSystem.Create("DebugMessage");
			signal.Message = "Hello World!";
			rsys.CreateCarrier(this, protocol, signal);
		}
    }
}
