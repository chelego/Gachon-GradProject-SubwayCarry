#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Delivery;
using SubwayCarry.Delivery.Mocks;
using SubwayCarry.Gameplay;
using SubwayCarry.UI;
using UnityEngine;

namespace SubwayCarry.Tests
{
    public sealed class PlayerDeliveryIntegrationTests
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
        public void PackageDurability_AbsorbsBoxDamageBeforeCake_AndResets()
        {
            PackageDurability durability =
                CreateGameObject("Package").AddComponent<PackageDurability>();

            durability.ApplyImpact(new PackageImpactData(null, Vector2.zero, 5f, 0f));

            Assert.That(durability.CurrentDurability.BoxDurability, Is.EqualTo(40f).Within(0.001f));
            Assert.That(durability.CurrentDurability.CakeDurability, Is.EqualTo(100f).Within(0.001f));

            durability.ApplyImpact(new PackageImpactData(null, Vector2.zero, 5f, 0f));

            Assert.That(durability.CurrentDurability.BoxDurability, Is.Zero.Within(0.001f));
            Assert.That(durability.CurrentDurability.CakeDurability, Is.EqualTo(80f).Within(0.001f));
            Assert.That(durability.CurrentDurability.DeliveryFailed, Is.False);

            durability.ResetToFull();

            Assert.That(durability.CurrentDurability.BoxDurability, Is.EqualTo(100f));
            Assert.That(durability.CurrentDurability.CakeDurability, Is.EqualTo(100f));
            Assert.That(durability.CurrentDurability.DeliveryFailed, Is.False);
        }

        [Test]
        public void EconomyService_AllowsZeroCostWithoutChangingCash()
        {
            EconomyService economy = CreateEconomy(15000);

            Assert.That(economy.TrySpend(0), Is.True);
            Assert.That(economy.CurrentEconomyState.CurrentCash, Is.EqualTo(15000));
        }

        [Test]
        public void UpgradePurchase_AppliesCumulativeLevelEffectToPlayerImmediately()
        {
            EconomyService economy = CreateEconomy(15000);
            UpgradeShopService shop =
                economy.gameObject.AddComponent<UpgradeShopService>();

            UpgradeData staminaData = CreateUpgradeData(
                UpgradeStat.Stamina,
                CreateUpgradeLevel(1, 1000, 15f),
                CreateUpgradeLevel(2, 2000, 30f));
            UpgradeData balanceData = CreateUpgradeData(
                UpgradeStat.Balance,
                CreateUpgradeLevel(1, 1200, 10f));
            UpgradeData agilityData = CreateUpgradeData(
                UpgradeStat.Agility,
                CreateUpgradeLevel(1, 1500, 5f));

            SetField(shop, "economyService", economy);
            SetField(
                shop,
                "catalog",
                new List<UpgradeData> { staminaData, balanceData, agilityData });

            GameObject player = CreatePlayer();
            PlayerPosture posture = player.GetComponent<PlayerPosture>();
            PlayerBalance balance = player.GetComponent<PlayerBalance>();
            PlayerController controller = player.GetComponent<PlayerController>();
            InvokePrivate(posture, "Awake");

            PlayerUpgradeEffectBinder binder =
                economy.gameObject.AddComponent<PlayerUpgradeEffectBinder>();
            binder.Configure(shop, posture, balance, controller);

            Assert.That(shop.TryPurchase(UpgradeStat.Stamina), Is.True);
            Assert.That(posture.EffectiveMaxStamina, Is.EqualTo(5.75f).Within(0.001f));

            Assert.That(shop.TryPurchase(UpgradeStat.Stamina), Is.True);
            Assert.That(posture.EffectiveMaxStamina, Is.EqualTo(6.5f).Within(0.001f));
            Assert.That(shop.GetCurrentEffectValue(UpgradeStat.Stamina), Is.EqualTo(30f));

            Assert.That(shop.TryPurchase(UpgradeStat.Balance), Is.True);
            Assert.That(balance.BalanceAssistPercent, Is.EqualTo(10f));

            Assert.That(shop.TryPurchase(UpgradeStat.Agility), Is.True);
            Assert.That(controller.EffectiveMoveSpeed, Is.EqualTo(3.675f).Within(0.001f));
            Assert.That(economy.CurrentEconomyState.CurrentCash, Is.EqualTo(9300));
        }

        [Test]
        public void DeliveryComplete_UndamagedPackageUpdatesSettlementEconomyAndHud()
        {
            DeliveryFixture fixture = CreateDeliveryFixture();
            GameplayHudPresenter hud =
                fixture.Service.gameObject.AddComponent<GameplayHudPresenter>();
            hud.Configure(
                fixture.Posture,
                null,
                fixture.Carrier,
                fixture.Service,
                fixture.Economy,
                fixture.Service);

            Assert.That(fixture.Service.TryStartDelivery("1-1"), Is.True);
            Assert.That(fixture.Carrier.HasPackage, Is.True);
            Assert.That(hud.CurrentDeliveryState.Phase, Is.EqualTo(DeliveryPhase.InTransit));
            Assert.That(hud.CurrentEconomyState.CurrentCash, Is.EqualTo(13500));

            fixture.Transit.ArriveAt("jeongja", true);

            Assert.That(fixture.Service.CurrentDeliveryState.Phase, Is.EqualTo(DeliveryPhase.Completed));
            Assert.That(fixture.Economy.CurrentEconomyState.CurrentCash, Is.EqualTo(16500));
            Assert.That(fixture.Carrier.HasPackage, Is.False);
            Assert.That(fixture.Service.LastSettlement.DeliveryFee, Is.EqualTo(3000));
            Assert.That(fixture.Service.LastSettlement.Compensation, Is.Zero);
            Assert.That(fixture.Service.LastSettlement.OutboundFare, Is.EqualTo(1500));
            Assert.That(fixture.Service.LastSettlement.ReturnFare, Is.Zero);
            Assert.That(fixture.Service.LastSettlement.NetIncome, Is.EqualTo(1500));
            Assert.That(hud.HasSettlement, Is.True);
            Assert.That(hud.CurrentSettlement.NetIncome, Is.EqualTo(1500));
        }

        [Test]
        public void DeliveryComplete_DamagedCakeChargesCompensationAndReturnFare()
        {
            DeliveryFixture fixture = CreateDeliveryFixture();

            Assert.That(fixture.Service.TryStartDelivery("1-1"), Is.True);
            fixture.Durability.ApplyImpact(
                new PackageImpactData(null, Vector2.zero, 10f, 0f));
            fixture.Transit.ArriveAt("jeongja", true);

            Assert.That(fixture.Durability.CurrentDurability.CakeDurability, Is.EqualTo(80f));
            Assert.That(fixture.Service.CurrentDeliveryState.Phase, Is.EqualTo(DeliveryPhase.Completed));
            Assert.That(fixture.Service.LastSettlement.Compensation, Is.EqualTo(5000));
            Assert.That(fixture.Service.LastSettlement.ReturnFare, Is.EqualTo(1500));
            Assert.That(fixture.Service.LastSettlement.NetIncome, Is.EqualTo(-5000));
            Assert.That(fixture.Economy.CurrentEconomyState.CurrentCash, Is.EqualTo(10000));
        }

        [Test]
        public void DeliveryComplete_PurchasedServicesWaiveDamageAndReturnCharges()
        {
            DeliveryFixture fixture = CreateDeliveryFixture(true);

            Assert.That(
                fixture.ServiceShop.TryPurchase(SchoolServiceType.DeliveryInsurance),
                Is.True);
            Assert.That(
                fixture.ServiceShop.TryPurchase(SchoolServiceType.TransitFareSupport),
                Is.True);
            Assert.That(fixture.Service.TryStartDelivery("1-1"), Is.True);

            fixture.Durability.ApplyImpact(
                new PackageImpactData(null, Vector2.zero, 10f, 0f));
            fixture.Transit.ArriveAt("jeongja", true);

            Assert.That(fixture.Service.LastSettlement.Compensation, Is.Zero);
            Assert.That(fixture.Service.LastSettlement.ReturnFare, Is.Zero);
            Assert.That(fixture.Service.LastSettlement.NetIncome, Is.EqualTo(1500));
            Assert.That(fixture.Economy.CurrentEconomyState.CurrentCash, Is.EqualTo(15800));
            Assert.That(
                fixture.ServiceShop.GetOwnedCount(SchoolServiceType.DeliveryInsurance),
                Is.Zero);
            Assert.That(
                fixture.ServiceShop.GetOwnedCount(SchoolServiceType.TransitFareSupport),
                Is.Zero);
        }

        [Test]
        public void DeliveryFailure_DestroyedCakeTriggersBankruptcyReset()
        {
            DeliveryFixture fixture = CreateDeliveryFixture();

            Assert.That(fixture.Service.TryStartDelivery("1-1"), Is.True);
            fixture.Durability.ApplyImpact(
                new PackageImpactData(null, Vector2.zero, 20f, 0f));

            Assert.That(fixture.Service.CurrentDeliveryState.Phase, Is.EqualTo(DeliveryPhase.Failed));
            Assert.That(
                fixture.Service.CurrentDeliveryState.FailureReason,
                Is.EqualTo(DeliveryFailureReason.Bankrupt));
            Assert.That(fixture.Service.LastSettlement.Compensation, Is.EqualTo(25000));
            Assert.That(fixture.Service.LastSettlement.ReturnFare, Is.EqualTo(1500));
            Assert.That(fixture.Service.LastSettlement.NetIncome, Is.EqualTo(-28000));
            Assert.That(fixture.Economy.CurrentEconomyState.CurrentCash, Is.EqualTo(15000));
            Assert.That(fixture.Carrier.HasPackage, Is.False);
        }

        private DeliveryFixture CreateDeliveryFixture(bool includeServices = false)
        {
            EconomyService economy = CreateEconomy(15000);
            MockTransitProgressProvider transit =
                economy.gameObject.AddComponent<MockTransitProgressProvider>();
            DeliveryService service =
                economy.gameObject.AddComponent<DeliveryService>();
            SchoolServiceShop serviceShop = null;

            if (includeServices)
            {
                serviceShop = economy.gameObject.AddComponent<SchoolServiceShop>();
                SetField(serviceShop, "economyService", economy);
                SetField(
                    serviceShop,
                    "catalog",
                    new List<ServiceData>
                    {
                        CreateServiceData(
                            SchoolServiceType.DeliveryInsurance,
                            400),
                        CreateServiceData(
                            SchoolServiceType.TransitFareSupport,
                            300)
                    });
            }

            GameObject player = CreatePlayer();
            PlayerPosture posture = player.GetComponent<PlayerPosture>();
            InvokePrivate(posture, "Awake");
            PlayerPackageCarrier carrier =
                player.GetComponent<PlayerPackageCarrier>();

            GameObject package = CreateGameObject("CakePackage");
            package.transform.SetParent(player.transform, false);
            PackageDurability durability =
                package.AddComponent<PackageDurability>();
            carrier.SetPackage(package.transform);
            carrier.SetPackageAvailable(false);

            DeliveryData data = CreateDeliveryData();
            SetField(service, "catalog", new List<DeliveryData> { data });
            SetField(service, "economyService", economy);
            SetField(service, "serviceShop", serviceShop);
            SetField(service, "packageDurabilityProviderSource", null);
            SetField(service, "packageAvailabilitySource", carrier);
            SetField(service, "transitProgressProviderSource", transit);
            InvokePrivate(service, "Awake");

            return new DeliveryFixture(
                economy,
                transit,
                service,
                serviceShop,
                posture,
                carrier,
                durability);
        }

        private EconomyService CreateEconomy(int initialCash)
        {
            EconomyService economy =
                CreateGameObject("Economy").AddComponent<EconomyService>();
            SetField(economy, "initialCash", initialCash);
            InvokePrivate(economy, "Awake");
            return economy;
        }

        private GameObject CreatePlayer()
        {
            GameObject player = CreateGameObject("Player");
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<PlayerPosture>();
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerBalance>();
            player.AddComponent<PlayerPackageCarrier>();
            return player;
        }

        private DeliveryData CreateDeliveryData()
        {
            DeliveryData data = ScriptableObject.CreateInstance<DeliveryData>();
            createdObjects.Add(data);
            SetField(data, "deliveryId", "1-1");
            SetField(data, "destinationStationId", "jeongja");
            SetField(data, "packageValue", 25000);
            SetField(data, "deliveryFee", 3000);
            SetField(data, "outboundFare", 1500);
            SetField(data, "returnFare", 1500);
            return data;
        }

        private UpgradeData CreateUpgradeData(
            UpgradeStat stat,
            params UpgradeLevelEntry[] levels)
        {
            UpgradeData data = ScriptableObject.CreateInstance<UpgradeData>();
            createdObjects.Add(data);
            SetField(data, "stat", stat);
            SetField(data, "levels", new List<UpgradeLevelEntry>(levels));
            return data;
        }

        private ServiceData CreateServiceData(
            SchoolServiceType serviceType,
            int price)
        {
            ServiceData data = ScriptableObject.CreateInstance<ServiceData>();
            createdObjects.Add(data);
            SetField(data, "serviceType", serviceType);
            SetField(data, "price", price);
            return data;
        }

        private static UpgradeLevelEntry CreateUpgradeLevel(
            int level,
            int price,
            float effectValue)
        {
            UpgradeLevelEntry entry = new UpgradeLevelEntry();
            SetField(entry, "level", level);
            SetField(entry, "price", price);
            SetField(entry, "effectValue", effectValue);
            return entry;
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

        private readonly struct DeliveryFixture
        {
            public DeliveryFixture(
                EconomyService economy,
                MockTransitProgressProvider transit,
                DeliveryService service,
                SchoolServiceShop serviceShop,
                PlayerPosture posture,
                PlayerPackageCarrier carrier,
                PackageDurability durability)
            {
                Economy = economy;
                Transit = transit;
                Service = service;
                ServiceShop = serviceShop;
                Posture = posture;
                Carrier = carrier;
                Durability = durability;
            }

            public EconomyService Economy { get; }
            public MockTransitProgressProvider Transit { get; }
            public DeliveryService Service { get; }
            public SchoolServiceShop ServiceShop { get; }
            public PlayerPosture Posture { get; }
            public PlayerPackageCarrier Carrier { get; }
            public PackageDurability Durability { get; }
        }
    }
}
#endif
