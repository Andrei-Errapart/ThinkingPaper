open System
open System.Collections.Generic
open System.Data.Linq.SqlClient
open System.Linq
open System.Text.RegularExpressions
open System.Windows
open System.Windows.Controls

open FSharpx
open Microsoft.FSharp.Data.TypeProviders
open Microsoft.FSharp.Linq

//===========================================================================
let rng = new Random()

//===========================================================================
type XamlMain = XAML<"WindowMain.xaml">
type XamlChangePassword = XAML<"WindowChangePassword.xaml">
type Schema = SqlDataConnection<"Data Source=.\SqlExpress;Initial Catalog=Digipuuk;Integrated Security=True">

//===========================================================================
let TitleOk = "SuperManager"
let TitleError = TitleOk + " - Error"
let TitleConfirm = TitleOk + " - Confirm"

//===========================================================================
type DigipuukUser() =
    member val Id = Guid.Empty with get,set
    member val UserName = "" with get, set
    member val EMail = "" with get, set
    member val LastLogin = DateTime.MinValue with get, set

//===========================================================================
let hash_password (pass:string) (salt:byte[]) =
    let bytes = System.Text.Encoding.Unicode.GetBytes(pass)
    let dst = Array.create (salt.Length + bytes.Length) 0uy
    Buffer.BlockCopy(salt, 0, dst, 0, salt.Length)
    Buffer.BlockCopy(bytes, 0, dst, salt.Length, bytes.Length)
    let algorithm = System.Security.Cryptography.HashAlgorithm.Create("SHA1")
    let inArray = algorithm.ComputeHash(dst)
    Convert.ToBase64String(inArray)

//===========================================================================
type WindowChangePassword (w:XamlChangePassword, username:string) as self =
    do
        w.buttonOK.Click.Add(self.OK)
        w.buttonCancel.Click.Add(self.Cancel)
        w.textboxUsername.Text <- username
    member this.Window = w.Root
    member this.OK (args:RoutedEventArgs) =
        this.Window.DialogResult <- new Nullable<bool>(true)
        this.Window.Close()
    member this.Cancel (args:RoutedEventArgs)=
        this.Window.DialogResult <- new Nullable<bool>(false)
        this.Window.Close()
    static member ShowDialog(username:string) =
        let x = XamlChangePassword ()
        let w = new WindowChangePassword(x, username)
        let r = w.Window.ShowDialog()
        if r.HasValue && r.Value then Some (x.textboxPassword.Text) else None

//===========================================================================
type WindowMain (db:Schema.ServiceTypes.SimpleDataContextTypes.Digipuuk, w:XamlMain) as self =
    let ListViewSelectedUsers () = seq { for o in w.listviewUsers.SelectedItems do yield o :?> DigipuukUser }
    do
        w.buttonSearch.Click.Add(self.Search)
        w.buttonDelete.Click.Add(self.Delete)
        w.buttonChangePassword.Click.Add(self.ChangePassword)
    member this.Window = w.Root
    member val DigipuukUsers = ([] :> IEnumerable<DigipuukUser>) with get, set
    member this.Search (args:RoutedEventArgs) =
        let txt = "%" + w.textboxEmail.Text + "%"
        this.DigipuukUsers <- (query { for m in db.Aspnet_Membership do
                                        where (SqlMethods.Like(m.Email, txt))
                                        let u = m.Aspnet_Users
                                        sortBy u.UserName
                                        let x = new DigipuukUser(Id=u.UserId, UserName=u.UserName, EMail=m.Email, LastLogin=m.LastLoginDate)
                                        select x }).ToArray()
        w.listviewUsers.ItemsSource <- this.DigipuukUsers
        ()
    member this.Delete (args:RoutedEventArgs) =
        let su = ListViewSelectedUsers ()
        let n = su.Count()
        if n=0 then
            MessageBox.Show("Select some users first!", TitleError) |> ignore
        else
            let r = MessageBox.Show("Do you really want to delete these " + (n.ToString()) + " users?", TitleConfirm, MessageBoxButton.YesNo)
            if r = MessageBoxResult.Yes then
                for u in su do
                    let id = u.Id
                    db.Aspnet_Profile.DeleteAllOnSubmit(query { for p in db.Aspnet_Profile do where (p.UserId=id); select p })
                    db.Aspnet_UsersInRoles.DeleteAllOnSubmit(query { for r in db.Aspnet_UsersInRoles do where (r.UserId=id); select r})
                    db.Aspnet_Membership.DeleteAllOnSubmit(query { for m in db.Aspnet_Membership do where (m.UserId=id); select m})
                    db.Aspnet_Users.DeleteOnSubmit(query { for u2 in db.Aspnet_Users do where (u2.UserId=id); select u2; exactlyOne})
                    db.DataContext.SubmitChanges()
                    ()
                ()
                // reload the search :)
                this.Search(args)
    member this.ChangePassword (args:RoutedEventArgs) =
        let su = ListViewSelectedUsers ()
        let n = su.Count()
        if n=0 then
            MessageBox.Show("Select one user first!", TitleError) |> ignore
        else if n=1 then
            let u = su.First ()
            let r = WindowChangePassword.ShowDialog u.UserName
            match r with
            | Some password ->
                let salt = Array.create 16 0uy
                rng.NextBytes(salt)
                let hash = hash_password password salt
                let m = query { for m2 in db.Aspnet_Membership do where (m2.UserId=u.Id); select m2; exactlyOne}
                // Avoid conflict if the record has been changed in the database.
                db.Aspnet_Membership.Context.Refresh(Data.Linq.RefreshMode.KeepCurrentValues, m)
                m.Password <- hash
                m.PasswordSalt <- Convert.ToBase64String(salt)
                db.Aspnet_Membership.Context.SubmitChanges()
            | None -> ()
        else
            MessageBox.Show("Select only one user!", TitleError) |> ignore
    new (db:Schema.ServiceTypes.SimpleDataContextTypes.Digipuuk) =
        // let pm = new System.Web.Profile.ProfileManager()
        WindowMain (db, XamlMain ())

//===========================================================================
let unhandled_exception_handler (args: System.Windows.Threading.DispatcherUnhandledExceptionEventArgs) =
    args.Handled <- true
    MessageBox.Show(args.Exception.Message, TitleError) |> ignore

//===========================================================================
[<EntryPoint>]
[<STAThread>]
let main argv = 
    try
        let db = Schema.GetDataContext()
        let app = new Application()
        app.DispatcherUnhandledException.Add(unhandled_exception_handler)
        let wm = new WindowMain (db)
        app.Run(wm.Window) |> ignore
    with
        | ex -> MessageBox.Show("Error:" + ex.Message, "Error") |> ignore
    0
