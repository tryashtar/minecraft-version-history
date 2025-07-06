namespace MinecraftVersionHistory;

public class Sided<T> where T : new()
{
    public readonly T Client;
    public readonly T Server;
    public Sided()
    {
        Client = new();
        Server = new();
    }
    public Sided(T client, T server)
    {
        Client = client;
        Server = server;
    }
}
