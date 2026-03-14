using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Exceptions;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Inquiries.Domain.Entities;

public sealed class Inquiry : AggregateRoot<InquiryId>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Company { get; private set; }
    public string? SubmitterId { get; private set; }
    public string ProjectType { get; private set; } = string.Empty;
    public string BudgetRange { get; private set; } = string.Empty;
    public string Timeline { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public InquiryStatus Status { get; private set; }
    public string SubmitterIpAddress { get; private set; } = string.Empty;

    private Inquiry() { } // EF Core

    public static Inquiry Create(
        string name,
        string email,
        string phone,
        string? company,
        string? submitterId,
        string projectType,
        string budgetRange,
        string timeline,
        string message,
        string submitterIpAddress,
        TimeProvider timeProvider)
    {
        Inquiry inquiry = new()
        {
            Id = InquiryId.New(),
            Name = name,
            Email = email,
            Phone = phone,
            Company = company,
            SubmitterId = submitterId,
            ProjectType = projectType,
            BudgetRange = budgetRange,
            Timeline = timeline,
            Message = message,
            SubmitterIpAddress = submitterIpAddress,
            Status = InquiryStatus.New
        };

        inquiry.SetCreated(timeProvider.GetUtcNow());

        inquiry.RaiseDomainEvent(new InquirySubmittedDomainEvent(
            inquiry.Id.Value,
            name,
            email,
            phone,
            company,
            submitterId,
            projectType,
            budgetRange,
            timeline,
            message));

        return inquiry;
    }

    public void TransitionTo(InquiryStatus newStatus, TimeProvider timeProvider)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidInquiryStatusTransitionException(
                Status.ToString(),
                newStatus.ToString());
        }

        InquiryStatus oldStatus = Status;
        Status = newStatus;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new InquiryStatusChangedDomainEvent(
            Id.Value,
            oldStatus.ToString(),
            newStatus.ToString()));
    }

    private static bool IsValidTransition(InquiryStatus current, InquiryStatus target) =>
        (current, target) switch
        {
            (InquiryStatus.New, InquiryStatus.Reviewed) => true,
            (InquiryStatus.Reviewed, InquiryStatus.Contacted) => true,
            (InquiryStatus.Contacted, InquiryStatus.Closed) => true,
            _ => false
        };
}
