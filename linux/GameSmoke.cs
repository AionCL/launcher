using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AionCL;

// Interactive, bounded GPU smoke test. No credentials, character login or patches.
class GameSmoke {
    static int Main(string[] args) {
        try { Run(args).GetAwaiter().GetResult(); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static async Task Run(string[] args) {
        if(args.Length!=2)throw new ArgumentException("Usage: AionCL.GameSmoke.exe <client-folder> <seconds:30..300>");
        int seconds;
        if(!Int32.TryParse(args[1],out seconds)||seconds<30||seconds>300)throw new ArgumentException("Duration must be 30..300 seconds.");
        string root=Path.GetFullPath(args[0]);
        VoiceMode.RequireGameClosed();
        var config=Json.Parse<LauncherConfig>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"launcher.json")));
        config.Validate();
        using(var cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(2)))
        using(var network=new Network(config.requestTimeoutSeconds)) {
            var feed=await Updates.Fetch(network,config.updateFeedUrl,cancellation.Token);
            config=Updates.Target(config,feed);
            var manifest=await network.ManifestAsync(config,cancellation.Token);
            var game=new GameLauncher(config);
            game.ValidateInstallation(root,manifest);
            var server=await new ServerService(network).Load(config.serverConfigUrl,root,Console.WriteLine,cancellation.Token);
            var address=await game.ResolveServer(server,cancellation.Token);
            await ServerService.Tcp(address,server.loginPort,cancellation.Token);
            var command=game.BuildLaunchCommand(root,server,address);
            // Defensive: this harness must never accept embedded account arguments.
            if(command.Arguments.IndexOf("-account",StringComparison.OrdinalIgnoreCase)>=0 ||
               command.Arguments.IndexOf("-password",StringComparison.OrdinalIgnoreCase)>=0)
                throw new InvalidDataException("Credentials are forbidden in this smoke test.");
            Console.WriteLine("START_NO_CREDENTIALS " + command.FileName);
            using(var process=game.StartGame(command)) {
                Console.WriteLine("GAME_PID="+process.Id);
                try {
                    for(int elapsed=0;elapsed<seconds;elapsed+=5) {
                        await Task.Delay(5000);
                        if(process.HasExited)throw new Exception("Game exited early: "+process.ExitCode);
                        Console.WriteLine("GAME_ALIVE_SECONDS="+(elapsed+5));
                    }
                    Console.WriteLine("PROCESS_STABILITY_PASS (visual rendering must be checked separately)");
                } finally {
                    if(!process.HasExited) { process.Kill(); process.WaitForExit(); }
                    Console.WriteLine("TEST_PROCESS_CLOSED");
                }
            }
        }
    }
}
