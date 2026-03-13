using System.Diagnostics;
using Foundry.Storage.Application.Telemetry;

namespace Foundry.Storage.Tests.Application.Telemetry;

public class StorageModuleTelemetryTests
{
    [Fact]
    public void ActivitySource_IsNotNull()
    {
        StorageModuleTelemetry.ActivitySource.Should().NotBeNull();
    }

    [Fact]
    public void Meter_IsNotNull()
    {
        StorageModuleTelemetry.Meter.Should().NotBeNull();
    }

    [Fact]
    public void OperationsTotal_IsNotNull()
    {
        StorageModuleTelemetry.OperationsTotal.Should().NotBeNull();
    }

    [Fact]
    public void BytesTransferred_IsNotNull()
    {
        StorageModuleTelemetry.BytesTransferred.Should().NotBeNull();
    }

    [Fact]
    public void OperationDuration_IsNotNull()
    {
        StorageModuleTelemetry.OperationDuration.Should().NotBeNull();
    }

    [Fact]
    public void StartUploadActivity_ReturnsActivityOrNull()
    {
        // Activity may return null if no listener is registered, which is valid
        Activity? activity = StorageModuleTelemetry.StartUploadActivity();
        activity?.Dispose();
    }

    [Fact]
    public void StartDownloadActivity_ReturnsActivityOrNull()
    {
        Activity? activity = StorageModuleTelemetry.StartDownloadActivity();
        activity?.Dispose();
    }

    [Fact]
    public void StartDeleteActivity_ReturnsActivityOrNull()
    {
        Activity? activity = StorageModuleTelemetry.StartDeleteActivity();
        activity?.Dispose();
    }

    [Fact]
    public void OperationsTotal_HasCorrectName()
    {
        StorageModuleTelemetry.OperationsTotal.Name.Should().Be("foundry.storage.operations_total");
    }

    [Fact]
    public void BytesTransferred_HasCorrectName()
    {
        StorageModuleTelemetry.BytesTransferred.Name.Should().Be("foundry.storage.bytes_transferred");
    }

    [Fact]
    public void OperationDuration_HasCorrectName()
    {
        StorageModuleTelemetry.OperationDuration.Name.Should().Be("foundry.storage.operation_duration");
    }
}
