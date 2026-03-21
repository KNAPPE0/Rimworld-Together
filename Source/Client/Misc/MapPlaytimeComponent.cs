using UnityEngine;
using Verse;

namespace GameClient.Misc
{
    public class MapPlaytimeComponent : MapComponent
    {
        private double _totalSeconds = 0;

        private float _lastRealtime;
        private bool _hasLast;

        public double TotalSeconds => _totalSeconds;

        public MapPlaytimeComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _totalSeconds, "rwt_realPlaytimeSeconds", 0d);
        }

        public override void MapComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.TickManager == null) return;

            if (Find.TickManager.Paused)
            {
                _hasLast = false;
                return;
            }

            float now = Time.realtimeSinceStartup;

            if (!_hasLast)
            {
                _lastRealtime = now;
                _hasLast = true;
                return;
            }

            float delta = now - _lastRealtime;

            if (delta < 0f || delta > 5f)
            {
                _lastRealtime = now;
                return;
            }

            _totalSeconds += delta;
            _lastRealtime = now;
        }
    }
}