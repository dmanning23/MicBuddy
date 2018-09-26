using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MicBuddyLib
{
	public class MicrophoneComponent : GameComponent, IMicrophoneComponent
	{
		#region Properties

		/// <summary>
		/// The default microhpohe to use
		/// </summary>
		public string DefaultMicName { get; private set; }

		/// <summary>
		/// The default mic sensitivity to use
		/// </summary>
		public float DefaultSensitvity { get; set; }

		/// <summary>
		/// A list of all the available microphones
		/// </summary>
		public List<string> AvailableMicrophones { get; private set; }

		/// <summary>
		/// Map all the mic names to microphone instances
		/// </summary>
		public Dictionary<string, Microphone> Microphones { get; private set; }

		private const float StartMicSensitvity = 0.05f;

		#endregion //Properties

		#region Methods

		/// <summary>
		/// initialize the static fields
		/// </summary>
		public MicrophoneComponent(Game game) : base(game)
		{
			game.Components.Add(this);
			game.Services.AddService<IMicrophoneComponent>(this);
		}

		public override void Initialize()
		{
			base.Initialize();

			//Set the default microhpone to the built-in mic
			Microphone defaultMicrophone = Microphone.Default;
			if (null == defaultMicrophone)
			{
				defaultMicrophone = Microphone.All.FirstOrDefault();
			}
			DefaultMicName = Microphone.Default?.Name;

			//set the default mic sensitivity
			DefaultSensitvity = StartMicSensitvity;

			// Add available capture devices to the combo box.
			AvailableMicrophones = new List<string>();
			Microphones = new Dictionary<string, Microphone>();
			foreach (var dude in Microphone.All)
			{
				if (!String.IsNullOrEmpty(dude.Name))
				{
					AvailableMicrophones.Add(dude.Name);
					Microphones.Add(dude.Name, dude);
				}
			}
		}

		#endregion Methods
	}
}