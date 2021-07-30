using DefaultEcs;
using HarmonyLib;
using RealisticBleeding.Components;
using ThunderRoad;
using UnityEngine;

namespace RealisticBleeding
{
	public static class EffectInstancePatches
	{
		[HarmonyPatch(typeof(EffectInstance), "AddEffect")]
		public static class AddEffectPatch
		{
			private static Vector3 _lastSpawnPosition = Vector3.positiveInfinity;
			
			public static void Postfix(EffectData effectData, Vector3 position, Quaternion rotation, Transform parent,
				CollisionInstance collisionInstance)
			{
				if (!Options.allowGore) return;

				if (collisionInstance == null) return;
				var ragdollPart = collisionInstance.damageStruct.hitRagdollPart;

				if (ragdollPart == null) return;
				
				var creature = ragdollPart.ragdoll.creature;

				var pressureIntensity = Catalog.GetCollisionStayRatio(collisionInstance.pressureRelativeVelocity.magnitude);

				var damageType = collisionInstance.damageStruct.damageType;
				if (damageType == DamageType.Unknown || damageType == DamageType.Energy) return;

				const float minBluntIntensity = 0.45f;
				const float minSlashIntensity = 0.01f;
				const float minPierceIntensity = 0.001f;

				var intensity = Mathf.Max(collisionInstance.intensity, pressureIntensity);

				var minIntensity = damageType == DamageType.Blunt ? minBluntIntensity :
					damageType == DamageType.Pierce ? minPierceIntensity : minSlashIntensity;
				if (intensity < minIntensity) return;

				if (damageType == DamageType.Blunt)
				{
					intensity *= 0.5f;
				}
				else if (damageType == DamageType.Pierce)
				{
					intensity *= 2.5f;
				}

				var multiplier = Mathf.Lerp(0.6f, 1.5f, Mathf.InverseLerp(minIntensity, 1, intensity));

				var durationMultiplier = multiplier;
				var frequencyMultiplier = multiplier;
				var sizeMultiplier = multiplier;

				switch (ragdollPart.type)
				{
					case RagdollPart.Type.Neck:
						durationMultiplier *= 5;
						frequencyMultiplier *= 5;
						sizeMultiplier *= 1.2f;
						break;
					case RagdollPart.Type.Head:
						if (damageType != DamageType.Blunt)
						{
							durationMultiplier *= 2f;
							frequencyMultiplier *= 3;
							sizeMultiplier *= 0.9f;
						}

						break;
					case RagdollPart.Type.Torso:
						if (damageType != DamageType.Blunt)
						{
							durationMultiplier *= 2f;
							frequencyMultiplier *= 4;
						}

						break;
				}

				Vector2 dimensions = new Vector2(0.01f, 0.01f);

				if (damageType == DamageType.Slash)
				{
					dimensions = new Vector2(0, Mathf.Lerp(0.06f, 0.12f, intensity));
				}

				if (damageType == DamageType.Blunt && ragdollPart.type == RagdollPart.Type.Head)
				{
					if (EntryPoint.Configuration.NoseBleedsEnabled)
					{
						if (NoseBleed.TryGetNosePosition(creature, out var nosePosition))
						{
							if (Vector3.Distance(nosePosition, position) < 0.1f)
							{
								NoseBleed.SpawnOn(creature, 1, 1, 0.7f);

								return;
							}
						}

						if (collisionInstance.intensity > 0.5f)
						{
							NoseBleed.SpawnOnDelayed(creature, Random.Range(1f, 2), intensity, intensity, Mathf.Max(0.3f, intensity));
						}
					}
				}

				if (EntryPoint.Configuration.MouthBleedsEnabled && damageType == DamageType.Pierce && ragdollPart.type == RagdollPart.Type.Torso)
				{
					if (intensity > 0.2f)
					{
						NoseBleed.SpawnOnDelayed(creature, Random.Range(0.8f, 1.7f), 0.1f, 0.05f, 0.4f);
						MouthBleed.SpawnOnDelayed(creature, Random.Range(0.8f, 1.7f), 1, 1);
					}
				}

				if (!EntryPoint.Configuration.BleedingFromWoundsEnabled) return;

				if (Vector3.Distance(position, _lastSpawnPosition) < 0.04f) return;

				_lastSpawnPosition = position;

				sizeMultiplier *= 0.75f;
				
				SpawnBleeder(position, rotation, collisionInstance.targetCollider.transform,
						durationMultiplier, frequencyMultiplier, sizeMultiplier, dimensions);
			}

			private static Entity SpawnBleeder(Vector3 position, Quaternion rotation, Transform parent,
				float durationMultiplier, float frequencyMultiplier, float sizeMultiplier, Vector2 dimensions)
			{
				return Bleeder.Spawn(parent, position, rotation, dimensions, frequencyMultiplier, sizeMultiplier, durationMultiplier);
			}
		}
	}
}