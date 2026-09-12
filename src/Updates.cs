using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AionCL {
public sealed class ClientRelease { public string version {get;set;} public string manifestUrl {get;set;} }
public sealed class LauncherRelease { public string version {get;set;} public string downloadPage {get;set;} }
public sealed class UpdateFeed {
    public int schemaVersion {get;set;} public string product {get;set;}
    public ClientRelease client {get;set;} public LauncherRelease launcher {get;set;}
    public void Validate() {
        if(schemaVersion!=1||product!="AionCL"||client==null||launcher==null)throw new InvalidDataException("Invalid update feed.");
        if(!Regex.IsMatch(client.version??"",@"^2\.4\.[0-9]+$"))throw new InvalidDataException("Unsupported client release.");
        Updates.Version(client.version);Updates.Version(launcher.version);Safety.Https(client.manifestUrl);Safety.Https(launcher.downloadPage);
    }
}
public static class Updates {
    public const string LauncherVersion = "1.1.2";
    public static Version Version(string value) {
        Version parsed;
        if(!Regex.IsMatch(value??"",@"^[0-9]+\.[0-9]+\.[0-9]+$")||!System.Version.TryParse(value,out parsed))throw new InvalidDataException("Invalid release version.");return parsed;
    }
    public static bool Newer(string remote,string local) {return Version(remote)>Version(local);}
    public static async Task<UpdateFeed> Fetch(Network network,string url,CancellationToken token) {
        var feed=Json.Parse<UpdateFeed>(Encoding.UTF8.GetString(await network.Small(url,token).ConfigureAwait(false)));
        if(feed==null)throw new InvalidDataException("Empty update feed.");feed.Validate();return feed;
    }
    public static LauncherConfig Target(LauncherConfig original,UpdateFeed feed) {
        feed.Validate();var target=Json.Parse<LauncherConfig>(Json.Serialize(original));
        if(Version(feed.client.version)<Version(original.clientBaseVersion))throw new InvalidDataException("Older client release refused.");
        target.clientBaseVersion=feed.client.version;target.manifestUrl=feed.client.manifestUrl;target.Validate();return target;
    }
}
}
