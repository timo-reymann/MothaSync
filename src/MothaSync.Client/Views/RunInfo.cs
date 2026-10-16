namespace MothaSync.Client.Views;

public sealed record RunInfo(
    string Date,
    string Files,
    string Size,
    string Duration,
    bool Failed,
    string LogText = "",
    long? RunId = null);
