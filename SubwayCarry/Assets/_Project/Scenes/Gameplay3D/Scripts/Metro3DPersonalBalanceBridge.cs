using System;
using System.Collections.Generic;
using SubwayCarry.Core.Contracts;
using SubwayCarry.Gameplay;
using UnityEngine;

namespace SubwayCarry.Gameplay3D
{
    [DefaultExecutionOrder(-5)]
    [DisallowMultipleComponent]
    public sealed class Metro3DPersonalBalanceBridge : MonoBehaviour, ITrainMotionProvider, IPlayerBalanceParticipant
    {
        [SerializeField] Metro3DPlayer player;

        readonly HashSet<Collider> ignoredPassengerColliders=new HashSet<Collider>();
        PlayerBalance balance;
        CharacterController playerController;
        float nextCollisionRefresh;
        int motionSequence;

        public TrainMotionSnapshot CurrentTrainMotion {get;private set;}
        public CarryPosture CurrentBalancePosture => player!=null?player.Posture:CarryPosture.Standing;
        public bool ConsumesMovementInput => balance!=null&&balance.ConsumesMovementInput;
        public bool NpcCollisionProtected => ConsumesMovementInput;
        public event Action<TrainMotionSnapshot> TrainMotionChanged;

        void Awake()
        {
            if(player==null) player=GetComponent<Metro3DPlayer>();
            playerController=GetComponent<CharacterController>();
            balance=GetComponent<PlayerBalance>();
            if(balance==null) balance=gameObject.AddComponent<PlayerBalance>();
            balance.Configure(this,this,player!=null?player.package:null);
            CurrentTrainMotion=new TrainMotionSnapshot(0,TrainMotionPhase.Stopped,Vector2.zero,0);

            var hud=GetComponent<Metro3DBalanceHud>();
            if(hud==null) hud=gameObject.AddComponent<Metro3DBalanceHud>();
            hud.Configure(player!=null?player.world:null,balance);
        }

        void Update()
        {
            if(balance!=null&&player!=null&&player.world!=null&&player.world.session!=null)
                balance.InputSuspended=player.world.session.Blocked;

            if(NpcCollisionProtected) {
                if(Time.unscaledTime>=nextCollisionRefresh) {
                    nextCollisionRefresh=Time.unscaledTime+.2f;
                    RefreshIgnoredPassengerCollisions();
                }
            } else if(ignoredPassengerColliders.Count>0) RestorePassengerCollisions();
        }

        public void PublishMotion(TrainMotionPhase phase,Vector2 inertiaDirection,float intensity)
        {
            if(player==null||player.world==null||player.world.session==null||
               player.Riding==null||player.Using!=null||!player.world.session.HasPackage) return;

            if(balance!=null&&player.world.settings!=null&&player.world.settings.deliveries!=null)
                balance.Assistance=player.world.session.BalanceLevel*player.world.settings.deliveries.balancePerLevel;

            CurrentTrainMotion=new TrainMotionSnapshot(++motionSequence,phase,inertiaDirection,intensity);
            TrainMotionChanged?.Invoke(CurrentTrainMotion);
        }

        public void FallFromBalance() { player?.ApplyBalanceFallResponse(); }
        public void CancelCurrentChallenge() { balance?.CancelCurrentChallenge(); }

        void RefreshIgnoredPassengerCollisions()
        {
            if(playerController==null||player==null||player.world==null) return;
            foreach(var passenger in player.world.passengers) {
                if(passenger==null) continue;
                foreach(var passengerCollider in passenger.GetComponentsInChildren<Collider>()) {
                    if(passengerCollider==null||ignoredPassengerColliders.Contains(passengerCollider)) continue;
                    Physics.IgnoreCollision(playerController,passengerCollider,true);
                    ignoredPassengerColliders.Add(passengerCollider);
                }
            }
        }

        void RestorePassengerCollisions()
        {
            if(playerController!=null)
                foreach(var passengerCollider in ignoredPassengerColliders)
                    if(passengerCollider!=null) Physics.IgnoreCollision(playerController,passengerCollider,false);
            ignoredPassengerColliders.Clear();
        }

        void OnDisable()
        {
            if(balance!=null) balance.CancelCurrentChallenge();
            RestorePassengerCollisions();
        }

        void OnDestroy() { RestorePassengerCollisions(); }
    }
}
