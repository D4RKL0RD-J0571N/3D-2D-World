using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey;

namespace CodeMonkey.Toolkit.TTickSystem {

    /// <summary>
    /// ** Tick System **
    /// 
    /// Easily run logic on a Tick rate
    /// 
    /// Most logic doesn't need to run on every single Update, just a few times per second is enough
    /// This Tick System keeps a tick rate and you can subscribe to events to run logic periodically
    /// 
    /// For example: Doing some FindTarget logic on your Enemy AI, no need to search 60 times per second
    /// Subscribe to the OnTick event and run 5 times per second, works the same and is much more efficient
    /// 
    /// Ticks use unscaledDeltaTime, if you want you can make it normal deltaTime, TickSystemMonoBehaviour
    /// 
    /// To setup: Place the Prefab in your scene, or call TickSystem.Create();
    /// </summary>
    public static class TickSystem {


        public static event EventHandler<OnTickEventArgs> OnTick;
        public static event EventHandler<OnTickEventArgs> OnTick_2;
        public static event EventHandler<OnTickEventArgs> OnTick_5;
        public static event EventHandler<OnTickEventArgs> OnTick_10;
        public class OnTickEventArgs : EventArgs {
            public int tick;
        }

        // Modify this if you want different tick rates, the default .2f equals 5 ticks per second.
        private const float TICK_TIMER_MAX = .2f;


        private static GameObject tickSystemGameObject;
        private static int tick;
        private static float tickTimer;


        public static void Create() {
            if (tickSystemGameObject == null) {
                tickSystemGameObject = new GameObject("TickSystem");
                tickSystemGameObject.AddComponent<TickSystemMonoBehaviour>();
            }
        }

        public static int GetTick() {
            return tick;
        }

        public static void ResetTick() {
            tick = 0;
        }

        public static void Update(float deltaTime) {
            tickTimer += Time.deltaTime;
            while (tickTimer >= TICK_TIMER_MAX) {
                tickTimer -= TICK_TIMER_MAX;
                AddTick();
            }
        }

        private static void AddTick() {
            tick++;
            if (OnTick != null) OnTick(null, new OnTickEventArgs { tick = tick });

            if (tick % 2 == 0) {
                if (OnTick_2 != null) OnTick_2(null, new OnTickEventArgs { tick = tick });
            }
            if (tick % 5 == 0) {
                if (OnTick_5 != null) OnTick_5(null, new OnTickEventArgs { tick = tick });
            }
            if (tick % 10 == 0) {
                if (OnTick_10 != null) OnTick_10(null, new OnTickEventArgs { tick = tick });
            }
        }

    }

}