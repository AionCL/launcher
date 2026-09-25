using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using AionCL;

// Runs only against an explicitly supplied recipe installation. No credentials.
class RecipeCheck {
    static int Main(string[] args) {
        try {
            if(args.Length!=3)throw new ArgumentException("Usage: RecipeCheck <client> <manifest.json> <verify|voices|extract>");
            string root=Path.GetFullPath(args[0]);
            VoiceMode.RequireGameClosed();
            var version=Json.Parse<LocalVersion>(File.ReadAllText(Safety.Under(root,".aioncl/version.json")));
            byte[] raw=File.ReadAllBytes(args[1]);
            if(Safety.Sha(raw)!=version.manifestHash)throw new InvalidDataException("Manifest does not match the installed version.");
            var manifest=Json.Parse<Manifest>(System.Text.Encoding.UTF8.GetString(raw));manifest.Validate(version.version);
            foreach(string lang in new[]{"FRA","ENG","DEU","RUS"}) {
                if(!File.Exists(Safety.Under(root,"L10N/"+lang+"/"+lang+".pak")))throw new FileNotFoundException(lang);
                Console.WriteLine("LANGUAGE_PRESENT="+lang);
            }
            var watch=Stopwatch.StartNew();
            if(args[2]=="verify") {
                var issues=Installation.Verify(root,manifest,CancellationToken.None).GetAwaiter().GetResult();
                foreach(var issue in issues)Console.WriteLine(issue.Path+": "+issue.Reason);
                if(issues.Count!=0)throw new InvalidDataException("Integrity failures: "+issues.Count);
                Console.WriteLine("FULL_INTEGRITY_PASS_SECONDS="+watch.Elapsed.TotalSeconds);
            } else if(args[2]=="voices") {
                var korean=KoreanPack.Load();var japanese=JapanesePack.Load();
                if(korean.HasPack(root,"FRA")||japanese.HasPack(root,"FRA"))throw new InvalidOperationException("Voice pack already active; recipe will not alter user selection.");
                try {
                    korean.Change(root,"FRA",true,null,null,CancellationToken.None).GetAwaiter().GetResult();
                    if(!korean.Valid(root,"FRA",CancellationToken.None).GetAwaiter().GetResult())throw new Exception("Korean validation failed");
                    Console.WriteLine("KOREAN_INSTALL_PASS");
                } finally { korean.Change(root,"FRA",false,null,null,CancellationToken.None).GetAwaiter().GetResult(); }
                try {
                    japanese.Change(root,"FRA",true,null,null,CancellationToken.None).GetAwaiter().GetResult();
                    if(!japanese.Valid(root,"FRA",CancellationToken.None).GetAwaiter().GetResult())throw new Exception("Japanese validation failed");
                    Console.WriteLine("JAPANESE_INSTALL_PASS");
                } finally { japanese.Change(root,"FRA",false,null,null,CancellationToken.None).GetAwaiter().GetResult(); }
                var font=HitFont.Load();
                if(!font.Installed(root,"FRA")) {
                    try {font.Change(root,"FRA",true);if(!font.Valid(root,"FRA"))throw new Exception("Hit font invalid");}
                    finally {font.Change(root,"FRA",false);}
                }
                Console.WriteLine("VOICE_REMOVE_AND_HIT_FONT_PASS_SECONDS="+watch.Elapsed.TotalSeconds);
            } else if(args[2]=="extract") {
                string scratch=Safety.Under(root,".aioncl/recipe-extract-"+Guid.NewGuid().ToString("N"));
                try {
                    LinuxPlatform.ExtractPackages(new InstallPlan {Packages=manifest.packages.Take(2).ToArray()},Safety.Under(root,".aioncl/cache"),scratch,CancellationToken.None,null,Console.WriteLine).GetAwaiter().GetResult();
                    Console.WriteLine("TWO_PACKAGES_EXTRACT_SHA_PASS_SECONDS="+watch.Elapsed.TotalSeconds);
                } finally { if(Directory.Exists(scratch))Directory.Delete(scratch,true); }
            } else throw new ArgumentException("Unknown recipe mode.");
            return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
