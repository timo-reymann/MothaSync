using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MothaSync.Server.Protobuf;
using MothaSync.Shared.Contracts;

namespace MothaSync.Server.Test;

public sealed class ProtobufFormattersTests
{
    private static InputFormatterContext CreateInputContext(HttpContext httpContext, Type modelType)
    {
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType);
        return new InputFormatterContext(
            httpContext,
            modelName: string.Empty,
            new ModelStateDictionary(),
            metadata,
            readerFactory: static (_, _) => TextReader.Null);
    }

    private static OutputFormatterWriteContext CreateOutputContext(HttpContext httpContext, object model)
    {
        return new OutputFormatterWriteContext(
            httpContext,
            writerFactory: static (_, _) => TextWriter.Null,
            objectType: model.GetType(),
            model);
    }

    [Fact]
    public void ProtobufDefaults_ContentTypeIsApplicationXProtobuf()
    {
        Assert.Equal("application/x-protobuf", ProtobufDefaults.ContentType);
    }

    [Fact]
    public void InputFormatter_SupportsProtobufContentType()
    {
        var formatter = new ProtobufInputFormatter();

        Assert.Contains(ProtobufDefaults.ContentType, formatter.SupportedMediaTypes);
    }

    [Fact]
    public void InputFormatter_CanReadProtobufMessagesOnly()
    {
        var formatter = new ProtobufInputFormatter();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = ProtobufDefaults.ContentType;

        Assert.True(formatter.CanRead(CreateInputContext(httpContext, typeof(SyncRun))));
        Assert.False(formatter.CanRead(CreateInputContext(httpContext, typeof(string))));
        Assert.False(formatter.CanRead(CreateInputContext(httpContext, typeof(int))));
    }

    [Fact]
    public async Task ReadRequestBodyAsync_ValidBytes_ParsesMessage()
    {
        var formatter = new ProtobufInputFormatter();
        var expected = new SyncRun
        {
            Id = Guid.NewGuid().ToString(),
            Status = SyncRunStatus.Completed,
            FilesChanged = 3,
        };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(expected.ToByteArray());

        var result = await formatter.ReadRequestBodyAsync(CreateInputContext(httpContext, typeof(SyncRun)));

        Assert.True(result.IsModelSet);
        Assert.Equal(expected, result.Model);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_MalformedBytes_ReturnsFailure()
    {
        var formatter = new ProtobufInputFormatter();
        var httpContext = new DefaultHttpContext();
        // Truncated varint field: tag byte without a value.
        httpContext.Request.Body = new MemoryStream(new byte[] { 0x08 });

        var result = await formatter.ReadRequestBodyAsync(CreateInputContext(httpContext, typeof(SyncRun)));

        Assert.False(result.IsModelSet);
    }

    [Fact]
    public void OutputFormatter_SupportsProtobufContentType()
    {
        var formatter = new ProtobufOutputFormatter();

        Assert.Contains(ProtobufDefaults.ContentType, formatter.SupportedMediaTypes);
    }

    [Fact]
    public async Task WriteResponseBodyAsync_WritesSerializedMessageBytes()
    {
        var formatter = new ProtobufOutputFormatter();
        var message = new RegisterDeviceResponse
        {
            DeviceId = Guid.NewGuid().ToString(),
            DeviceToken = "token",
        };
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await formatter.WriteResponseBodyAsync(CreateOutputContext(httpContext, message));

        Assert.Equal(message.ToByteArray(), ((MemoryStream)httpContext.Response.Body).ToArray());
    }
}
