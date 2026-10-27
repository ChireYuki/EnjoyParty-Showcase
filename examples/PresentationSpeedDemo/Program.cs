using System;
using LuckyStarParty.Pve;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Check("Unanimity schedules; the next player turn applies", () =>
        {
            var vote = new PvePresentationSpeedVote();
            Require(vote.Propose(true, 0, 5, 15, 10), "proposal rejected");
            for (int seat = 1; seat < 4; seat++) Require(vote.Vote(vote.Id, seat, true, 11), "vote rejected");
            Require(vote.Result == PresentationVoteResult.Scheduled && vote.Rate == 1, "applied mid-turn");
            Require(!vote.ObserveTurn(1, 0, true, 12), "same turn applied");
            Require(!vote.ObserveTurn(1, 1, false, 13), "non-boundary applied");
            Require(vote.ObserveTurn(1, 1, true, 14) && vote.Rate == 5, "next turn failed");
        });
        Check("A rejection keeps the old rate", () =>
        {
            var vote = new PvePresentationSpeedVote();
            vote.Propose(true, 0, 2, 3, 0);
            Require(vote.Vote(vote.Id, 1, false, 1), "rejection rejected");
            Require(vote.Result == PresentationVoteResult.Rejected && vote.Rate == 1, "rejected rate applied");
        });
        Check("Expired proposals cannot pass with a late vote", () =>
        {
            var vote = new PvePresentationSpeedVote();
            vote.Propose(true, 0, 5, 3, 100);
            Require(!vote.Vote(vote.Id, 1, true, 115), "late vote accepted");
            Require(vote.Result == PresentationVoteResult.Expired && vote.Rate == 1, "expiry failed");
        });
        Check("Changing participants cancels an ongoing vote", () =>
        {
            var vote = new PvePresentationSpeedVote();
            vote.Propose(true, 0, 2, 3, 0); vote.Poll(1, 1);
            Require(vote.Result == PresentationVoteResult.Cancelled && vote.Rate == 1, "threshold silently lowered");
        });
        Check("Old IDs, duplicate voters and invalid rates are rejected", () =>
        {
            var vote = new PvePresentationSpeedVote();
            Require(!vote.Propose(false, 0, 2, 3, 0), "non-host proposal accepted");
            Require(!vote.Propose(true, 0, 3, 3, 0), "unsupported rate accepted");
            Require(vote.Propose(true, 0, 2, 3, 0), "valid proposal rejected");
            Require(!vote.Vote(vote.Id - 1, 1, true, 1), "stale ID accepted");
            Require(!vote.Vote(vote.Id, 0, true, 1), "duplicate accepted");
        });
        Check("Seat gaps do not require votes from AI seats", () =>
        {
            var vote = new PvePresentationSpeedVote();
            Require(vote.Propose(true, 0, 2, 5, 0), "proposal rejected");
            Require(!vote.Vote(vote.Id, 1, true, 1), "ineligible seat voted");
            Require(vote.Vote(vote.Id, 2, true, 1), "eligible seat rejected");
            Require(vote.Result == PresentationVoteResult.Scheduled, "human unanimity missed");
        });
        Check("Long turns do not expire an approved schedule", () =>
        {
            var vote = new PvePresentationSpeedVote();
            vote.Propose(true, 0, 2, 1, 0); vote.Poll(1, 1000);
            Require(vote.Result == PresentationVoteResult.Scheduled && vote.Rate == 1, "schedule lost");
            Require(vote.ObserveTurn(1, 1, true, 1001), "schedule not applied");
        });
        Check("Cancelling a scheduled change never applies it", () =>
        {
            var vote = new PvePresentationSpeedVote();
            vote.Propose(true, 0, 5, 1, 0); vote.Cancel(1);
            Require(!vote.ObserveTurn(2, 0, true, 2) && vote.Rate == 1, "cancelled change applied");
        });
        Check("The presentation clock remains continuous", () =>
        {
            var clock = new PvePresentationTimeline();
            clock.Advance(100); clock.Advance(101); clock.SetRate(5, 101); clock.Advance(102);
            Require(clock.Now == 6, "first rate interval incorrect");
            clock.SetRate(2, 102.5); clock.Advance(104);
            Require(clock.Now == 11.5, "previous time rescaled");
        });
        Check("Invalid and backwards clock samples are ignored", () =>
        {
            var clock = new PvePresentationTimeline();
            clock.Advance(2); clock.Advance(1); clock.Advance(double.NaN); clock.Advance(double.PositiveInfinity); clock.Advance(3);
            Require(clock.Now == 1, "invalid sample changed clock");
        });
        Console.WriteLine(failures == 0 ? "All 10 showcase checks passed." : "Failed checks: " + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void Check(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
