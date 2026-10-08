namespace AcxiomCrm.Models
{
    public enum CustomerStatus
    {
        Active = 1,
        Inactive = 2,
        Pending = 3
    }

    public enum LeadStatus
    {
        New = 1,
        Contacted = 2,
        Qualified = 3,
        Unqualified = 4,
        Converted = 5,
        Lost = 6
    }

    public enum OpportunityStage
    {
        Prospecting = 1,
        Qualification = 2,
        Proposal = 3,
        Negotiation = 4,
        ClosedWon = 5,
        Won = 5,
        ClosedLost = 6,
        Lost = 6
    }

    public enum FollowUpType
    {
        Call = 1,
        Meeting = 2,
        Email = 3,
        Task = 4
    }

    public enum FollowUpStatus
    {
        Planned = 1,
        Pending = 1,
        Completed = 2,
        Cancelled = 3,
        Missed = 4
    }

    public enum PriorityLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Urgent = 4
    }
}
