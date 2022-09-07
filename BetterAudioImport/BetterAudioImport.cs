using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Threading.Tasks;

namespace ModNameGoesHere
{
    public class BetterAudioImport : NeosMod
    {
        public override string Name => "BetterAudioImport";
        public override string Author => "dfgHiatus";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/BetterSoundImport";
        private static ModConfiguration config;

        [AutoRegisterConfigKey]
		private static ModConfigurationKey<AudioTypes> audioType =
			new("audioType",
			"Format to convert all new audio to.",
			() => AudioTypes.OGG);

		public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.BetterAudioImport").PatchAll();
			config = GetConfiguration();
		}

		private static Uri uriCopy;
		
        [HarmonyPatch(typeof(AudioPlayerOrb), "OnAttach")]
        class AudioPlayerOrbPatch
        {
            public static void Postfix(AudioPlayerOrb __instance)
            {
				AudioEncodeSettings settings = new VorbisEncodeSettings();
				switch (config.GetValue(audioType))
				{
					case AudioTypes.OGG:
						settings = new VorbisEncodeSettings();
						break;
					case AudioTypes.FLAC:
						settings = new FlacEncodeSettings();
						break;
					case AudioTypes.WAV:
						settings = new WavEncodeSettings();
						break;
					default:
						settings = new VorbisEncodeSettings();
						break;
				}

				// TODO Wait until the audio is loaded

				if (__instance.AudioClip.Asset.Data.EncodeSettings != settings)
                {
					var uriField = __instance.Slot.GetComponent<StaticAudioClip>().URL.Value;
					Process((AudioX a) => a, __instance.AudioClip.Asset, uriField, settings);
					uriField = uriCopy;
				}
			}

            private static void Process(Func<AudioX, AudioX> processFunc, AudioClip clip, Uri uriField, AudioEncodeSettings encodeSettings)
            {
				Engine.Current.WorldManager.FocusedWorld.Coroutines.StartTask(async delegate
				{
					await ProcessAsync(processFunc, clip, uriField, encodeSettings);
				});
			}

			private static async Task ProcessAsync(Func<AudioX, AudioX> processFunc, AudioClip clip, Uri uriField, AudioEncodeSettings encodeSettings = null)
			{
				if (uriField == null)
					return;

				while (clip == null)
					await default(NextUpdate);

				Uri uri;

				try
				{
					AudioX audio = processFunc(await clip.GetOriginalAudioData().ConfigureAwait(continueOnCapturedContext: false));
					uri = await Engine.Current.LocalDB.SaveAssetAsync(audio, encodeSettings).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex)
				{
					UniLog.Error($"Exception processing audio clip {uriField}:\n" + ex);
					await default(ToWorld);
					throw;
				}

				await default(ToWorld);

				if (!(uri == null))
					uriCopy = uri;
			}
		}
    }
}