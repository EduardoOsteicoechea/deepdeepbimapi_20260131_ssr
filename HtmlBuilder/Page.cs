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
        </head>
        <body>
        <h1>DeepDeepBIM</h1>
        """u8;

    public static async Task Print(System.IO.Pipelines.PipeWriter writer)
    {        
        writer.Write(Head);
        await writer.CompleteAsync();
    }

}