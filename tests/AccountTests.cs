using System;
using System.IO;
using AionCL;
using System.Reflection;
using System.Windows.Forms;
public static class AccountTests {
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    [STAThread]
    public static void Main() {
        var root=Path.Combine(Path.GetTempPath(),"aioncl-account-test-"+Guid.NewGuid().ToString("N"));
        var previous=Environment.GetEnvironmentVariable("AIONCL_CONFIG_DIR");
        Environment.SetEnvironmentVariable("AIONCL_CONFIG_DIR",root);
        try {
            var auth=new LauncherAuth();
            Check(auth.SelectedAccount==null,"Empty account list");
            auth.SaveCredentials("hetimop","fake-one",true);
            auth.SaveCredentials("blackzanpakto","fake-two",true);
            auth.SelectAccount("HETIMOP");
            auth=new LauncherAuth();
            Check(auth.SelectedAccount=="hetimop","Last selection restored after restart");
            Check(auth.Accounts[0].username=="blackzanpakto","Selecting does not reorder accounts");
            Check(auth.Find(auth.SelectedAccount).password=="fake-one","Selected credentials restored");
            auth.SaveCredentials("third","fake-three",true);
            Check(auth.SelectedAccount=="hetimop","Saving another account does not replace selection");
            auth.SelectAccount("absent");
            Check(auth.SelectedAccount=="hetimop","Unknown selection ignored");
            auth.ForgetCredential("blackzanpakto");
            Check(auth.SelectedAccount=="hetimop","Deleting another account preserves selection");
            auth.ForgetCredential("hetimop");
            Check(new LauncherAuth().SelectedAccount=="third","Deleted selection falls back to remaining account");
            auth.SaveCredentials("hetimop","fake-new",true);
            auth.SelectAccount("third");
            auth.SaveCredentials("HETIMOP","fake-replaced",true);
            Check(auth.Accounts.Count==2 && auth.Find("hetimop").password=="fake-replaced","Case-insensitive replacement");
            Application.EnableVisualStyles();
            using(var form=new MainForm(true)) {
                var type=typeof(MainForm);
                var refresh=type.GetMethod("RefreshAccountBox",BindingFlags.Instance|BindingFlags.NonPublic);
                var box=(ComboBox)type.GetField("accountBox",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var user=(TextBox)type.GetField("authUser",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var password=(TextBox)type.GetField("authPassword",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                refresh.Invoke(form,new object[]{null});
                Check(box.SelectedItem.ToString()=="third" && user.Text=="third" && password.Text=="fake-three","UI restores selected account and credentials");
                box.SelectedItem="HETIMOP";
                Check(new LauncherAuth().SelectedAccount=="HETIMOP" && password.Text=="fake-replaced","Dropdown persists selection and fills credentials");
                var manual=(Button)type.GetField("authManualButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                Check(manual.Text=="Sauvegarder ces identifiants","Same explicit save action on both platforms");
                user.Text="fourth";password.Text="fake-four";
                var save=(System.Threading.Tasks.Task)type.GetMethod("ManualAuthenticateAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                while(!save.IsCompleted) { Application.DoEvents(); System.Threading.Thread.Sleep(1); }
                save.GetAwaiter().GetResult();
                Check(new LauncherAuth().SelectedAccount=="fourth" && box.SelectedItem.ToString()=="fourth","Save action persists and selects new account");
                auth.ForgetCredential("fourth");refresh.Invoke(form,new object[]{null});
                Check(box.SelectedItem.ToString()==user.Text && password.Text.Length>0,"Removal leaves remaining selection and fields consistent");
                type.GetMethod("SetBusy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{true});
                Check(!box.Enabled && !user.Enabled && !manual.Enabled,"Account changes disabled during operation");
                auth.ForgetCredentials();refresh.Invoke(form,new object[]{null});
                Check(box.SelectedIndex==-1 && user.Text=="" && password.Text=="","Last removal clears account fields");
            }
            auth.ForgetCredentials();
            Check(new LauncherAuth().SelectedAccount==null && auth.Accounts.Count==0,"Delete all accounts and selection");
            Console.WriteLine("PASS accounts: persistence, selection, replacement, removal and fallback");
        } finally {
            Environment.SetEnvironmentVariable("AIONCL_CONFIG_DIR",previous);
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
