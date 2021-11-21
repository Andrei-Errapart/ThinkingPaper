// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.


#r "FSharp.Data.TypeProviders.dll"
#r "System.Data.Linq.dll"
#load @"..\packages\DotNetZip.1.9.1.8\lib\net20\Ionic.Zip.dll"
#r @"..\packages\DotNetZip.1.9.1.8\lib\net20\Ionic.Zip.dll"
#load @"..\packages\HtmlAgilityPack.1.4.6\lib\Net40\HtmlAgilityPack.dll"
#r @"..\packages\HtmlAgilityPack.1.4.6\lib\Net40\HtmlAgilityPack.dll"
#load "EPub.fs"

open ThinkingPaper

// Define your library scripting code here
let my_epub = new EPub("EpubTemplate.zip", 396, System.Guid.Parse("BCB8D032-DE3D-45B8-9132-3EE4F5BB57DF"), ".")

my_epub.Save(my_epub.Filename)

