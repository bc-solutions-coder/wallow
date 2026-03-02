using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Tests.Common.Builders;

public class SmsMessageBuilder
{
    private TenantId _tenantId = TenantId.New();
    private PhoneNumber _to = PhoneNumber.Create("+15551234567");
    private PhoneNumber? _from;
    private string _body = "Test SMS message";
    private bool _sent;
    private bool _failed;
    private string _failureReason = "Delivery failed";

    public SmsMessageBuilder WithTenantId(TenantId tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public SmsMessageBuilder WithTo(string phoneNumber)
    {
        _to = PhoneNumber.Create(phoneNumber);
        return this;
    }

    public SmsMessageBuilder WithFrom(string phoneNumber)
    {
        _from = PhoneNumber.Create(phoneNumber);
        return this;
    }

    public SmsMessageBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public SmsMessageBuilder AsSent()
    {
        _sent = true;
        return this;
    }

    public SmsMessageBuilder AsFailed(string reason = "Delivery failed")
    {
        _failed = true;
        _failureReason = reason;
        return this;
    }

    public SmsMessage Build()
    {
        SmsMessage smsMessage = SmsMessage.Create(_tenantId, _to, _from, _body);

        if (_sent)
        {
            smsMessage.MarkAsSent();
        }

        if (_failed)
        {
            smsMessage.MarkAsFailed(_failureReason);
        }

        smsMessage.ClearDomainEvents();

        return smsMessage;
    }

    public static SmsMessageBuilder Create() => new();
}
