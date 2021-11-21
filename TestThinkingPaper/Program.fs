// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
open Microsoft.FSharp.Data.TypeProviders
open System.Data.Linq.SqlClient
open System.Linq
open Microsoft.FSharp.Linq

open ThinkingPaper

type schema = SqlDataConnection<"Data Source=.\SqlExpress;Initial Catalog=Digipuuk;Integrated Security=True">

let create_all_epubs () =
    let rids =
        use db = schema.GetDataContext()
        query { for r in db.Raamatud do select (r.RID, r.Pealkiri) } |> Seq.toList
    for rid,title in rids do
        printfn "EPUB %d: %s" rid title
        let my_epub = new EPub("EpubTemplate.zip", rid, System.Guid.Parse("BCB8D032-DE3D-45B8-9132-3EE4F5BB57DF"), ".", "http://www.e-raamatukogu.com/")
        printfn "Writing: %s ..." my_epub.Filename
        use file = System.IO.File.OpenWrite(my_epub.Filename)
        my_epub.Save file
    ()

let create_1_epub (rid:int) =
    printfn "Creating EPUB..."
    let my_epub = new EPub("EpubTemplate.zip", rid, System.Guid.Parse("BCB8D032-DE3D-45B8-9132-3EE4F5BB57DF"), ".", "http://www.e-raamatukogu.com/")
    printfn "Storing EPUB as %s ..." my_epub.Filename
    use file = System.IO.File.OpenWrite(my_epub.Filename)
    my_epub.Save(file)
    ()

[<EntryPoint>]
let main argv = 
    if argv.Length=0 then
        printfn "Usage: TestThinkingPaper (RID1|all) [RID2 ... RIDN]"
        printfn "where RID: book ID in the database."
    for arg in argv do
        if arg.ToLowerInvariant() = "all" then
            create_all_epubs ()
        else
            try
                printfn "Processing book rid='%s'..." arg
                let rid = System.Int32.Parse(arg)
                create_1_epub rid
            with
            | ex -> printfn "Error for the book id '%s': %s" arg (ex.Message)
    // printfn "%A" argv
    // printfn "Creating EPUB..."
    // 422 = Humoristide lips ei ole veel läbi
    // 12 = Tiputähelood
    // 132 = jutumärgid pealkirjas.
    // 146 = puudub kaanepilt.
    // 239 = koolon pealkirjas.
    // create_all_epubs ()
    // create_1_epub 2
    // printfn "Finished!"
    0 // return an integer exit code
