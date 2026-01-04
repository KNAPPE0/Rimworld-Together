using GameClient;
using GameClient.Core.Configs;
using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using Shared;
using Shared.Misc;
using Synchronous.Misc;
using Synchronous.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Verse;
using Verse.AI;

namespace Synchronous.Managers
{
    public static class SJobManager
    {
        private static List<PlayerJob> PlayerJobs { get; set; } = new List<PlayerJob>();

        private static double UpdateTimer { get; set; } = -1;

        [OnSessionStart]
        private static void Initialize() 
        { 
            PlayerJobs = new List<PlayerJob>();
            UpdateTimer = 0;
        }

        [OnUpdate]
        private static void Check()
        {
            if (UpdateTimer < 1f) UpdateTimer += Time.deltaTime;
            else
            {
                if (PlayerJobs.Count > 0)
                {
                    ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SPlayerJob, Serializer.ConvertObjectToBytes(PlayerJobs));
                    PlayerJobs.Clear();
                }

                UpdateTimer = 0;
            }
        }

        public static void Ask(Job job, Pawn pawn)
        {
            if (PlayerJobs.FirstOrDefault(fetch => fetch.PawnID == pawn.ThingID) != null) return;

            PlayerJob newJob = new PlayerJob();
            newJob.MapTile = pawn.MapHeld.Tile;
            newJob.PawnID = pawn.ThingID;
            newJob.Job = ScribeManager.SerializeToString(job, ScribeManager.SerializableType.Other);

            //playerJob.PawnPosition = pawn.Position;
            //playerJob.GlobalTarget = job.globalTarget;
            //playerJob.TargetA = job.GetTarget(TargetIndex.A);
            //playerJob.TargetB = job.GetTarget(TargetIndex.B);
            //playerJob.TargetC = job.GetTarget(TargetIndex.C);
            //playerJob.QueueA = job.GetTargetQueue(TargetIndex.A);
            //playerJob.QueueB = job.GetTargetQueue(TargetIndex.B);
            //playerJob.JobScribe = ScribeHandler.ConvertToScribe(job);

            PlayerJobs.Add(newJob);

            PatchHandler.ExecuteInBypass(delegate
            {
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 1000),
                JobCondition.InterruptForced);
            });
        }

        [HandlesPacket(PacketHeader.SPlayerJob)]
        private static void Receive(byte[] bytes)
        {
            PlayerJob[] jobs = Serializer.ConvertBytesToObject<PlayerJob[]>(bytes);

            PatchHandler.ExecuteInBypass(delegate
            {
                foreach (PlayerJob job in jobs)
                {
                    Map map = Finder.GetMapFromTile(job.MapTile);
                    Pawn pawn = Finder.GetPawnFromID(map, job.PawnID);
                }
            });
        }
    }
}
