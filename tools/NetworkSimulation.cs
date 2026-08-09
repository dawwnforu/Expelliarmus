using System;
using System.Collections.Generic;

namespace Expelliarmus.NetworkSimulation
{
    internal enum SyncPolicy
    {
        Fixed120Ms,
        HostConfirmed
    }

    internal sealed class Scenario
    {
        public string Name;
        public int RoundTripMs;
        public int JitterMs;
        public double LossRate;
        public int RetransmitMs;
    }

    internal sealed class ScheduledEvent
    {
        public int DueMs;
        public long Sequence;
        public Action Action;
    }

    internal sealed class TrialResult
    {
        public bool Success;
        public bool SyncTimeout;
        public bool InvalidPickupRace;
        public int CompletionMs;
    }

    internal sealed class Simulator
    {
        private const int SyncTimeoutMs = 3000;
        private const int PollIntervalMs = 50;

        private readonly Scenario scenario;
        private readonly SyncPolicy policy;
        private readonly bool casterStartsWithItem;
        private readonly Random random;
        private readonly List<ScheduledEvent> events = new List<ScheduledEvent>();

        private long nextSequence;
        private int nowMs;
        private bool hostCasterOldItem;
        private bool hostTargetItem = true;
        private bool hostCasterStolenItem;
        private bool hostOldItemOnGround;
        private bool clientCasterOldItem;
        private bool clientTargetItem = true;
        private bool clientCasterStolenItem;
        private bool pickupRequested;
        private bool invalidPickupRace;
        private bool syncTimeout;
        private int completionMs = -1;

        public Simulator(Scenario scenario, SyncPolicy policy, bool casterStartsWithItem, int seed)
        {
            this.scenario = scenario;
            this.policy = policy;
            this.casterStartsWithItem = casterStartsWithItem;
            random = new Random(seed);
            hostCasterOldItem = casterStartsWithItem;
            clientCasterOldItem = casterStartsWithItem;
        }

        public TrialResult Run()
        {
            if (casterStartsWithItem)
            {
                SendReliableToHost(HostDropCasterItem, 0);
            }
            SendReliableToHost(HostRemoveTargetItem, 0);

            if (policy == SyncPolicy.Fixed120Ms)
            {
                Schedule(120, RequestPickup);
            }
            else
            {
                Schedule(0, PollForAuthoritativeInventory);
            }

            int safetyDeadlineMs = 15000;
            while (events.Count > 0 && nowMs <= safetyDeadlineMs)
            {
                events.Sort(CompareEvents);
                ScheduledEvent next = events[0];
                events.RemoveAt(0);
                nowMs = next.DueMs;
                next.Action();

                if (completionMs < 0 && IsConverged())
                {
                    completionMs = nowMs;
                }
            }

            bool success = IsConverged();

            return new TrialResult
            {
                Success = success,
                SyncTimeout = syncTimeout,
                InvalidPickupRace = invalidPickupRace,
                CompletionMs = success ? completionMs : -1
            };
        }

        private bool IsConverged()
        {
            return clientCasterStolenItem &&
                   !clientTargetItem &&
                   !clientCasterOldItem &&
                   hostCasterStolenItem &&
                   !hostTargetItem &&
                   (!casterStartsWithItem || hostOldItemOnGround);
        }

        private static int CompareEvents(ScheduledEvent left, ScheduledEvent right)
        {
            int dueComparison = left.DueMs.CompareTo(right.DueMs);
            return dueComparison != 0 ? dueComparison : left.Sequence.CompareTo(right.Sequence);
        }

        private void HostDropCasterItem()
        {
            if (!hostCasterOldItem) return;
            hostCasterOldItem = false;
            hostOldItemOnGround = true;
            SendReliableToClient(delegate { clientCasterOldItem = false; }, 0);
        }

        private void HostRemoveTargetItem()
        {
            if (!hostTargetItem) return;
            hostTargetItem = false;
            SendReliableToClient(delegate { clientTargetItem = false; }, 0);
        }

        private void PollForAuthoritativeInventory()
        {
            bool localReady = !clientCasterOldItem;
            bool targetReady = !clientTargetItem;
            if (localReady && targetReady)
            {
                RequestPickup();
                return;
            }

            if (nowMs >= SyncTimeoutMs)
            {
                syncTimeout = true;
                return;
            }

            Schedule(PollIntervalMs, PollForAuthoritativeInventory);
        }

        private void RequestPickup()
        {
            if (pickupRequested) return;
            pickupRequested = true;
            SendReliableToHost(HostHandlePickup, 0);
        }

        private void HostHandlePickup()
        {
            if (hostTargetItem || hostCasterOldItem)
            {
                invalidPickupRace = true;
                return;
            }

            hostCasterStolenItem = true;
            SendReliableToClient(delegate { clientCasterStolenItem = true; }, 0);
        }

        private void SendReliableToHost(Action action, int extraDelayMs)
        {
            Schedule(NetworkDelayMs() + extraDelayMs, action);
        }

        private void SendReliableToClient(Action action, int extraDelayMs)
        {
            Schedule(NetworkDelayMs() + extraDelayMs, action);
        }

        private int NetworkDelayMs()
        {
            int oneWay = Math.Max(1, scenario.RoundTripMs / 2 + RandomJitter());
            int attempts = 0;
            while (random.NextDouble() < scenario.LossRate && attempts < 20)
            {
                oneWay += scenario.RetransmitMs + Math.Max(1, scenario.RoundTripMs / 2 + RandomJitter());
                attempts++;
            }
            return oneWay;
        }

        private int RandomJitter()
        {
            if (scenario.JitterMs <= 0) return 0;
            return random.Next(-scenario.JitterMs, scenario.JitterMs + 1);
        }

        private void Schedule(int delayMs, Action action)
        {
            events.Add(new ScheduledEvent
            {
                DueMs = nowMs + Math.Max(0, delayMs),
                Sequence = nextSequence++,
                Action = action
            });
        }
    }

    internal static class Program
    {
        private const int TrialsPerCase = 1000;

        private static int Main()
        {
            Scenario[] scenarios =
            {
                new Scenario { Name = "SameCity", RoundTripMs = 30, JitterMs = 10, LossRate = 0.001, RetransmitMs = 150 },
                new Scenario { Name = "CrossProvince", RoundTripMs = 100, JitterMs = 35, LossRate = 0.01, RetransmitMs = 220 },
                new Scenario { Name = "CrossCountry", RoundTripMs = 260, JitterMs = 90, LossRate = 0.03, RetransmitMs = 320 },
                new Scenario { Name = "PoorNetwork", RoundTripMs = 520, JitterMs = 220, LossRate = 0.08, RetransmitMs = 500 },
                new Scenario { Name = "Extreme", RoundTripMs = 900, JitterMs = 450, LossRate = 0.15, RetransmitMs = 900 }
            };

            Console.WriteLine("Trials per case: {0}", TrialsPerCase);
            Console.WriteLine("{0,-14} {1,-13} {2,-9} {3,8} {4,9} {5,9} {6,8} {7,8}",
                "Scenario", "Policy", "CasterOld", "Success%", "Timeouts", "Races", "P50ms", "P95ms");

            bool baselinePassed = true;
            int caseIndex = 0;
            foreach (Scenario scenario in scenarios)
            {
                foreach (SyncPolicy policy in new[] { SyncPolicy.Fixed120Ms, SyncPolicy.HostConfirmed })
                {
                    foreach (bool casterHasItem in new[] { false, true })
                    {
                        CaseStats stats = RunCase(scenario, policy, casterHasItem, caseIndex++);
                        Console.WriteLine("{0,-14} {1,-13} {2,-9} {3,8:F1} {4,9} {5,9} {6,8} {7,8}",
                            scenario.Name,
                            policy,
                            casterHasItem ? "yes" : "no",
                            stats.SuccessPercent,
                            stats.Timeouts,
                            stats.Races,
                            stats.P50Ms,
                            stats.P95Ms);

                        if (policy == SyncPolicy.HostConfirmed &&
                            scenario.Name != "Extreme" &&
                            stats.SuccessPercent < 99.0)
                        {
                            baselinePassed = false;
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(baselinePassed
                ? "PASS: host-confirmed policy met the reliability baseline."
                : "FAIL: host-confirmed policy missed the reliability baseline.");
            return baselinePassed ? 0 : 1;
        }

        private static CaseStats RunCase(Scenario scenario, SyncPolicy policy, bool casterHasItem, int caseIndex)
        {
            int successes = 0;
            int timeouts = 0;
            int races = 0;
            List<int> completionTimes = new List<int>();

            for (int trial = 0; trial < TrialsPerCase; trial++)
            {
                int seed = 7919 * (caseIndex + 1) + trial * 104729;
                TrialResult result = new Simulator(scenario, policy, casterHasItem, seed).Run();
                if (result.Success)
                {
                    successes++;
                    completionTimes.Add(result.CompletionMs);
                }
                if (result.SyncTimeout) timeouts++;
                if (result.InvalidPickupRace) races++;
            }

            completionTimes.Sort();
            return new CaseStats
            {
                SuccessPercent = successes * 100.0 / TrialsPerCase,
                Timeouts = timeouts,
                Races = races,
                P50Ms = Percentile(completionTimes, 0.50),
                P95Ms = Percentile(completionTimes, 0.95)
            };
        }

        private static int Percentile(List<int> values, double percentile)
        {
            if (values.Count == 0) return -1;
            int index = (int)Math.Ceiling(values.Count * percentile) - 1;
            return values[Math.Max(0, Math.Min(values.Count - 1, index))];
        }

        private sealed class CaseStats
        {
            public double SuccessPercent;
            public int Timeouts;
            public int Races;
            public int P50Ms;
            public int P95Ms;
        }
    }
}
