using System.Net;
using System.Net.Http.Json;
using Foundry.Communications.Api.Contracts.Messaging.Requests;
using Foundry.Communications.Api.Contracts.Messaging.Responses;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Factories;

namespace Foundry.Communications.Tests.Integration.Messaging;

[CollectionDefinition(nameof(CommunicationsIntegrationTestCollection))]
public class CommunicationsIntegrationTestCollection : ICollectionFixture<FoundryApiFactory>;

[Collection(nameof(CommunicationsIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ConversationIntegrationTests : FoundryIntegrationTestBase
{
    private const string BaseUrl = "/api/v1/conversations";

    public ConversationIntegrationTests(FoundryApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateConversation_WithValidRequest_Returns201Created()
    {
        SetTestUser(Guid.NewGuid().ToString());
        Guid participantId = Guid.NewGuid();
        CreateConversationRequest request = new(
            ParticipantIds: new List<Guid> { participantId },
            Subject: "Test conversation");

        HttpResponseMessage response = await Client.PostAsJsonAsync(BaseUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid conversationId = await response.Content.ReadFromJsonAsync<Guid>();
        conversationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SendMessage_ToExistingConversation_Returns201Created()
    {
        SetTestUser(Guid.NewGuid().ToString());
        Guid participantId = Guid.NewGuid();

        // Create a conversation first
        CreateConversationRequest createRequest = new(
            ParticipantIds: new List<Guid> { participantId },
            Subject: null);
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync(BaseUrl, createRequest);
        Guid conversationId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Send a message to the conversation
        SendMessageRequest messageRequest = new(Body: "Hello, world!");
        HttpResponseMessage response = await Client.PostAsJsonAsync($"{BaseUrl}/{conversationId}/messages", messageRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid messageId = await response.Content.ReadFromJsonAsync<Guid>();
        messageId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMessages_ForExistingConversation_ReturnsPagedList()
    {
        SetTestUser(Guid.NewGuid().ToString());
        Guid participantId = Guid.NewGuid();

        // Create conversation and send a message
        CreateConversationRequest createRequest = new(
            ParticipantIds: new List<Guid> { participantId },
            Subject: null);
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync(BaseUrl, createRequest);
        Guid conversationId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        SendMessageRequest messageRequest = new(Body: "Test message");
        await Client.PostAsJsonAsync($"{BaseUrl}/{conversationId}/messages", messageRequest);

        // Get messages
        HttpResponseMessage response = await Client.GetAsync($"{BaseUrl}/{conversationId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        MessagePageResponse? page = await response.Content.ReadFromJsonAsync<MessagePageResponse>();
        page.Should().NotBeNull();
        page.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MarkAsRead_ForExistingConversation_Returns204NoContent()
    {
        SetTestUser(Guid.NewGuid().ToString());
        Guid participantId = Guid.NewGuid();

        // Create conversation
        CreateConversationRequest createRequest = new(
            ParticipantIds: new List<Guid> { participantId },
            Subject: null);
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync(BaseUrl, createRequest);
        Guid conversationId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Mark as read
        HttpResponseMessage response = await Client.PostAsync($"{BaseUrl}/{conversationId}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetConversations_AfterCreatingMultiple_ReturnsSortedByLastActivityDescending()
    {
        Guid userId = Guid.NewGuid();
        SetTestUser(userId.ToString());

        // Create two conversations
        CreateConversationRequest firstRequest = new(
            ParticipantIds: new List<Guid> { Guid.NewGuid() },
            Subject: "First conversation");
        await Client.PostAsJsonAsync(BaseUrl, firstRequest);

        // Small delay to ensure different LastActivityAt timestamps
        await Task.Delay(50);

        CreateConversationRequest secondRequest = new(
            ParticipantIds: new List<Guid> { Guid.NewGuid() },
            Subject: "Second conversation");
        await Client.PostAsJsonAsync(BaseUrl, secondRequest);

        // Get conversations
        HttpResponseMessage response = await Client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<ConversationResponse>? conversations =
            await response.Content.ReadFromJsonAsync<List<ConversationResponse>>();
        conversations.Should().NotBeNull();
        conversations.Should().HaveCountGreaterThanOrEqualTo(2);

        // Verify sorted by LastActivityAt descending
        for (int i = 0; i < conversations.Count - 1; i++)
        {
            conversations[i].LastActivityAt.Should()
                .BeOnOrAfter(conversations[i + 1].LastActivityAt);
        }
    }
}
