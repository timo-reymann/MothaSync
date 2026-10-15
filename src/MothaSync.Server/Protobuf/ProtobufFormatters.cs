using Google.Protobuf;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace MothaSync.Server.Protobuf;

public static class ProtobufDefaults
{
    public const string ContentType = "application/x-protobuf";
}

public sealed class ProtobufInputFormatter : InputFormatter
{
    public ProtobufInputFormatter() => SupportedMediaTypes.Add(ProtobufDefaults.ContentType);

    protected override bool CanReadType(Type type) => typeof(IMessage).IsAssignableFrom(type);

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        using var buffer = new MemoryStream();
        await context.HttpContext.Request.Body.CopyToAsync(buffer);
        try
        {
            var message = (IMessage)Activator.CreateInstance(context.ModelType)!;
            message.MergeFrom(buffer.ToArray());
            return await InputFormatterResult.SuccessAsync(message);
        }
        catch (InvalidProtocolBufferException)
        {
            return await InputFormatterResult.FailureAsync();
        }
    }
}

public sealed class ProtobufOutputFormatter : OutputFormatter
{
    public ProtobufOutputFormatter() => SupportedMediaTypes.Add(ProtobufDefaults.ContentType);

    protected override bool CanWriteType(Type? type) => type is not null && typeof(IMessage).IsAssignableFrom(type);

    public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        var bytes = ((IMessage)context.Object!).ToByteArray();
        return context.HttpContext.Response.Body.WriteAsync(bytes).AsTask();
    }
}
