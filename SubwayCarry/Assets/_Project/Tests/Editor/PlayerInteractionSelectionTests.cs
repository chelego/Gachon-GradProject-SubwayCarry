#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SubwayCarry.AI.V2;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using SubwayCarry.Prototype.ArtMapSlice;
using UnityEngine;

namespace SubwayCarry.Tests
{
    public sealed class PlayerInteractionSelectionTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void OverlappingSeatAndSupport_CloserFacilityWinsRegardlessOfRegistrationOrder()
        {
            SliceGameController world = CreateWorld(out _);
            PassengerAiV2InteriorSpotSmartObject seat = CreateSpot(
                "Seat",
                PassengerAiV2InteriorSpotKind.Seat,
                new Vector2(0.8f, 0f));
            PassengerAiV2InteriorSpotSmartObject support = CreateSpot(
                "Support",
                PassengerAiV2InteriorSpotKind.Stand,
                new Vector2(0.4f, 0f));
            SetField(world, "spots", new[] { seat, support });

            Assert.That(world.TryGetNearestFacility(out Vector2 position, out CarryPosture target), Is.True);
            Assert.That(position, Is.EqualTo(support.UsePosition));
            Assert.That(target, Is.EqualTo(CarryPosture.HoldingSupport));
        }

        [Test]
        public void OverlappingSeatAndSupport_EqualDistancePrefersSeatAndOccupiesOnlySeat()
        {
            SliceGameController world = CreateWorld(out PlayerPosture posture);
            Vector2 sharedPosition = new Vector2(0.5f, 0f);
            PassengerAiV2InteriorSpotSmartObject support = CreateSpot(
                "Support",
                PassengerAiV2InteriorSpotKind.Stand,
                sharedPosition);
            PassengerAiV2InteriorSpotSmartObject seat = CreateSpot(
                "Seat",
                PassengerAiV2InteriorSpotKind.Seat,
                sharedPosition);

            // Support is deliberately registered first to prove hierarchy order cannot change the tie result.
            SetField(world, "spots", new[] { support, seat });

            Assert.That(world.TryGetNearestFacility(out Vector2 position, out CarryPosture hintedPosture), Is.True);
            Assert.That(position, Is.EqualTo(sharedPosition));
            Assert.That(hintedPosture, Is.EqualTo(CarryPosture.Sitting));

            Assert.That(world.TryUseNearestFacility(out CarryPosture selectedPosture), Is.True);
            Assert.That(selectedPosture, Is.EqualTo(CarryPosture.Sitting));
            Assert.That(posture.TargetState, Is.EqualTo(CarryPosture.Sitting));
            Assert.That(world.HasActivePlayerFacility, Is.True);
            Assert.That(seat.IsReserved, Is.True);
            Assert.That(seat.IsOccupied, Is.True);
            Assert.That(support.IsReserved, Is.False);
            Assert.That(support.IsOccupied, Is.False);
            Assert.That(
                posture.GetComponent<Rigidbody2D>().position,
                Is.EqualTo(sharedPosition),
                "The ground collider must be aligned with the selected facility use point.");

            posture.RestorePosture(CarryPosture.Sitting, 1f);
            Assert.That(world.TryReleasePlayerFacility(), Is.True);
            Assert.That(posture.TargetState, Is.EqualTo(CarryPosture.Standing));
            Assert.That(support.IsReserved, Is.False);
            Assert.That(support.IsOccupied, Is.False);

            // The selected seat stays owned during the stand-up transition, then is released.
            Assert.That(seat.IsOccupied, Is.True);
            posture.RestorePosture(CarryPosture.Standing, 1f);
            InvokePrivate(world, "ReleasePlayerFacility");
            Assert.That(world.HasActivePlayerFacility, Is.False);
            Assert.That(seat.IsReserved, Is.False);
            Assert.That(seat.IsOccupied, Is.False);
        }

        [Test]
        public void FacilityUse_AlignsGroundPositionAndFacesAwayFromFurniture()
        {
            SliceGameController world = CreateWorld(out PlayerPosture posture);
            PlayerController controller = posture.GetComponent<PlayerController>();
            SliceMap map = CreateGameObject("Map").AddComponent<SliceMap>();
            SliceSolidFootprint furniture = new SliceSolidFootprint
            {
                center = new Vector2(0.5f, 0.9f),
                halfSize = new Vector2(0.6f, 0.2f),
                slope = 0f
            };
            map.solidFootprints = new[] { furniture };
            world.maps = new[] { map };

            PassengerAiV2InteriorSpotSmartObject seat = CreateSpot(
                "Seat",
                PassengerAiV2InteriorSpotKind.Seat,
                new Vector2(0.5f, 0f));
            SetField(world, "spots", new[] { seat });

            Assert.That(world.TryUseNearestFacility(out CarryPosture target), Is.True);
            Assert.That(target, Is.EqualTo(CarryPosture.Sitting));
            Assert.That(posture.GetComponent<Rigidbody2D>().position, Is.EqualTo(seat.UsePosition));
            Assert.That(controller.FacingDirection.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(controller.FacingDirection.y, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(map.solidFootprints[0].center, Is.EqualTo(furniture.center));
        }

        private SliceGameController CreateWorld(out PlayerPosture posture)
        {
            GameObject player = CreateGameObject("Player");
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            posture = player.AddComponent<PlayerPosture>();
            InvokePrivate(posture, "Awake");
            body.position = Vector2.zero;
            PlayerController controller = player.AddComponent<PlayerController>();

            PassengerAiV2Agent proxy =
                CreateGameObject("PlayerProxy").AddComponent<PassengerAiV2Agent>();
            SliceGameController world =
                CreateGameObject("World").AddComponent<SliceGameController>();
            SetField(world, "playerBody", body);
            SetField(world, "posture", posture);
            SetField(world, "playerInput", controller);
            SetField(world, "playerProxy", proxy);
            return world;
        }

        private PassengerAiV2InteriorSpotSmartObject CreateSpot(
            string objectName,
            PassengerAiV2InteriorSpotKind kind,
            Vector2 position)
        {
            PassengerAiV2InteriorSpotSmartObject spot =
                CreateGameObject(objectName).AddComponent<PassengerAiV2InteriorSpotSmartObject>();
            spot.Configure(objectName, kind, position, 0.5f, 0.5f, null);
            return spot;
        }

        private GameObject CreateGameObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
            method.Invoke(target, null);
        }
    }
}
#endif
