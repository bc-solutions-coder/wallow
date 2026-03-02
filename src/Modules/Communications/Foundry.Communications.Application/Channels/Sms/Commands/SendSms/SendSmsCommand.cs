namespace Foundry.Communications.Application.Channels.Sms.Commands.SendSms;

public sealed record SendSmsCommand(
    string To,
    string Body);
