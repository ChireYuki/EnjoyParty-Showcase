using System;

namespace LuckyStarParty.Pve
{
    public enum PresentationVoteResult { None, Pending, Accepted, Rejected, Expired, Cancelled, Scheduled }

    // Cosmetic room policy, independent of turn ownership and the rules RNG.
    public sealed class PvePresentationSpeedVote
    {
        public int Id { get; private set; }
        public int Rate { get; private set; } = 1;
        public int Target { get; private set; } = 1;
        public int Eligible { get; private set; }
        public int Yes { get; private set; }
        public double Deadline { get; private set; }
        public PresentationVoteResult Result { get; private set; }
        private double nextProposalAt;
        private int currentRound = 1, currentSeat, scheduledRound, scheduledSeat;
        public double NextProposalAt => nextProposalAt;
        public static bool IsValidRate(int rate) => rate == 1 || rate == 2 || rate == 5;

        public bool Propose(bool isHost, int seat, int target, int humans, double now)
        {
            if (!isHost || !IsValidRate(target) || target == Rate || Result == PresentationVoteResult.Pending || Result == PresentationVoteResult.Scheduled ||
                seat < 0 || seat > 3 || humans <= 0 || (humans & ~15) != 0 ||
                (humans & (1 << seat)) == 0 || now < nextProposalAt || double.IsNaN(now)) return false;
            Id++; Target = target; Eligible = humans; Yes = 1 << seat;
            Deadline = now + 15; Result = PresentationVoteResult.Pending;
            CompleteIfUnanimous(now);
            return true;
        }

        public bool Vote(int id, int seat, bool agree, double now)
        {
            Poll(Eligible, now);
            if (Result != PresentationVoteResult.Pending || id != Id || seat < 0 || seat > 3 ||
                (Eligible & (1 << seat)) == 0 || (Yes & (1 << seat)) != 0) return false;
            if (!agree) Finish(PresentationVoteResult.Rejected, now);
            else { Yes |= 1 << seat; CompleteIfUnanimous(now); }
            return true;
        }

        public void Poll(int humans, double now)
        {
            if (Result != PresentationVoteResult.Pending) return;
            // A changed roster requires a fresh proposal, never silently reduces unanimity.
            if (humans != Eligible) Finish(PresentationVoteResult.Cancelled, now);
            else if (now >= Deadline) Finish(PresentationVoteResult.Expired, now);
        }

        public void Cancel(double now)
        { if (Result == PresentationVoteResult.Pending || Result == PresentationVoteResult.Scheduled) Finish(PresentationVoteResult.Cancelled, now); }

        // Called with the authoritative turn, never with a movement/camera event.
        // Re-entering PreMove on the same seat (skills, portals, sprint) is not a new turn.
        public bool ObserveTurn(int round, int seat, bool isTurnStart, double now)
        {
            currentRound = round; currentSeat = seat;
            if (Result != PresentationVoteResult.Scheduled || !isTurnStart || seat < 0 || seat > 3 ||
                (round == scheduledRound && seat == scheduledSeat)) return false;
            Rate = Target; Finish(PresentationVoteResult.Accepted, now); return true;
        }

        private void CompleteIfUnanimous(double now)
        {
            if (Yes != Eligible) return;
            scheduledRound = currentRound; scheduledSeat = currentSeat;
            Finish(PresentationVoteResult.Scheduled, now);
        }
        private void Finish(PresentationVoteResult result, double now)
        { Result = result; nextProposalAt = now + 3; }
    }

    public sealed class PvePresentationTimeline
    {
        public double Now { get; private set; }
        public int Rate { get; private set; } = 1;
        private double lastReal;
        private bool initialized;
        public void Advance(double real)
        {
            if (double.IsNaN(real) || double.IsInfinity(real)) return;
            if (!initialized) { lastReal = real; initialized = true; return; }
            if (real < lastReal) return;
            Now += (real - lastReal) * Rate; lastReal = real;
        }
        public void SetRate(int rate, double real)
        {
            if (!PvePresentationSpeedVote.IsValidRate(rate)) throw new ArgumentOutOfRangeException(nameof(rate));
            Advance(real); Rate = rate;
        }
    }
}
