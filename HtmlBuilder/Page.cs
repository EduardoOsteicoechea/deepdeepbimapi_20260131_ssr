using System.Buffers;

public static class Page
{
    public static ReadOnlySpan<byte> Head => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>DeepDeepBim.com</title>
            <link rel="stylesheet" href="static/global.css">
            <script src="static/global.js"></script>
        </head>
        <body>
        <h1>DeepDeepBIM</h1> 
        """u8;

    private static ReadOnlySpan<byte> GreetUserStart => "<p>Hello, "u8;
    private static ReadOnlySpan<byte> GreetUserEnd => "!</p>"u8;

    public static async Task Print(System.IO.Pipelines.PipeWriter writer, string userName)
    {
        writer.Write(Head);
        writer.Write(GreetUserStart);

        int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(userName.Length);
        Span<byte> userNameBuffer = writer.GetSpan(maxByteCount);
        int actualAmountOfBytesRequired = System.Text.Encoding.UTF8.GetBytes(userName, userNameBuffer);
        writer.Advance(actualAmountOfBytesRequired);

        writer.Write(GreetUserEnd);

        await writer.CompleteAsync();
    }

}