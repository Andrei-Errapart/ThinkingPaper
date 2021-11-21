// Learn more about F# at http://fsharp.net

open System.Net
open HtmlAgilityPack


let getWebPage (url:string) (post_data:string option) =
    let sb  = new System.Text.StringBuilder()
    let buf = Array.create 8192 (byte 0)
    let request  = (WebRequest.Create url) :?> HttpWebRequest
    if post_data.IsSome then
        request.Method <- "POST"
        // Create POST data and convert it to a byte array.
        let bytes = System.Text.Encoding.UTF8.GetBytes post_data.Value
        request.ContentType <- "application/x-www-form-urlencoded"
        request.ContentLength <- int64 bytes.Length
        let dataStream = request.GetRequestStream ()
        dataStream.Write (bytes, 0, bytes.Length);
        dataStream.Close ()
    let response = request.GetResponse() :?> HttpWebResponse
    let resStream = response.GetResponseStream()
    let count      = ref 1
    while !count > 0 do
        count := resStream.Read(buf, 0, buf.Length)
        if !count <> 0 then
            let tempString = System.Text.Encoding.ASCII.GetString(buf, 0, !count)
            sb.Append(tempString) |> ignore
    sb.ToString()

let banned_strings = [ "mailto"; "info@kodutud.com"; "@import "; "admin@leicestershirevillages.com" ]

let contains_banned_strings (s:string) = List.fold (fun d bs -> if d then d else s.Contains(bs)) false banned_strings

let fetch_emails (html:string) (char_before:char) (char_after) =
    let at_indices =
        let r = ref []
        String.iteri (fun i ch -> if ch='@' then r := i :: !r) html
        !r
    let emails =
        let fetch_1_email (at_index:int) =
            let l_before = html.Substring(0, at_index).Split([| char_before |])
            let l_after = html.Substring(at_index+1).Split([| char_after |])
            let before_at = if l_before.Length>0 then l_before.[l_before.Length-1] else ""
            let after_at = if l_after.Length>0 then l_after.[0] else ""
            before_at + "@" + after_at
        at_indices |> List.map fetch_1_email |> List.filter (fun s -> not (contains_banned_strings s))
    emails

let fetch_emails2 (doc:HtmlDocument) =
    let email_of_href (href:string) =
        let v = href.Split([| ':' |])
        if v.Length=2 && v.[0]="mailto" then Some (v.[1]) else None
    let emails =
        let seq_nodes = doc.DocumentNode.SelectNodes("//a[@href]")
        let nodes = if seq_nodes=null then [] else List.ofSeq (seq_nodes)
        seq { for n in nodes do
                let o_html = email_of_href (System.Web.HttpUtility.HtmlDecode (n.GetAttributeValue("href", "")))
                match o_html with
                    Some x -> yield x
                    | None -> () }
    emails

let fetch_user_urls (html:string) (prefix:string) =
    let urls = ref []
    let idx = ref 0
    while !idx >= 0 do
        let ss = html.Substring(!idx)
        let ss_idx = ss.IndexOf(prefix)
        if ss_idx >= 0 then
            let ss2 = if ss_idx > 0 then ss.Substring(ss_idx) else ss
            let q_pos = ss2.IndexOf('"')
            if q_pos>prefix.Length then
                let new_url = ss2.Substring(0, q_pos)
                if not (List.exists (fun s -> new_url=s) !urls) then
                    urls := new_url :: !urls
                idx := !idx + ss_idx + q_pos
            else
                idx := !idx + ss_idx + prefix.Length
        else
            idx := -1
    !urls |> List.rev

let fetch_web1 () =
    use fout = System.IO.File.CreateText("web1.txt")
    for i=0 to 30000 do
        try
            let html = getWebPage ("http://www.kodutud.com/member.php?member=" + i.ToString()) None
            let emails = fetch_emails html '>' '<'
            emails |> List.iter (fun s -> printfn "%i -> %s" i s)
            emails |> List.iter (fun s -> fout.WriteLine(s) )
        with
            | ex -> printfn "Exception: %s" (ex.ToString() )
        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 1.0)
    fout.Flush()
    printfn "Finished."

let fetch_web2 () =
    use fout = System.IO.File.CreateText("web-swchq.txt")
    for i=0 to 25 do
        try
            let html = getWebPage ("http://swchq.co.uk/swcforum/memberlist.php?start=" + (i*50).ToString()) None
            let emails = fetch_emails html ':' '"'
            emails |> List.iter (fun s -> printfn "%i -> %s" i s)
            emails |> List.iter (fun s -> fout.WriteLine(s) )
        with
            | ex -> printfn "Exception: %s" (ex.ToString() )
        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 2.0)
    fout.Flush()
    printfn "Finished."

let fetch_web3 () =
    use fout = System.IO.File.CreateText("web3.txt")
    let (used_urls:string list ref) = ref []
    for i=1 to 84 do
        try
            let html = getWebPage ("http://foorum.noortele.ee/?page=user_list&action=" + i.ToString()) None
            let urls = fetch_user_urls html "http://foorum.noortele.ee/user/"
            printfn "Fetched %d users from page %d" (List.length urls) i
            let handle_url (url:string) =
                if not (List.exists (fun s -> s = url) !used_urls) then
                    used_urls := url :: !used_urls
                    try
                        let html = getWebPage url None
                        let emails = fetch_emails html '>' '<'
                        emails |> List.iter (fun s -> printfn "%s -> %s" url s)
                        emails |> List.iter (fun s -> fout.WriteLine(s) )
                        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 2.0)
                    with
                        | ex -> printfn "Exception: %s" (ex.ToString() )
            List.iter handle_url urls
        with
            | ex -> printfn "Exception: %s" (ex.ToString() )
        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 2.0)
    fout.Flush()
    printfn "Finished."

let fetch_web4 () =
    use fout = System.IO.File.CreateText("web4.txt")
    // 1120
    // <div id="viewauthor_authorName" class="heading">Rekha Waheed</div>
    let author_name_of_doc (doc:HtmlDocument) =
        let seq_nodes = doc.DocumentNode.SelectNodes("//div[@id]")
        let nodes = if seq_nodes=null then [] else List.ofSeq (seq_nodes)
        let author_names = seq { for n in nodes do
                                    if n.GetAttributeValue("id", "") = "viewauthor_authorName" then
                                        yield n.InnerText }
        match List.ofSeq author_names with
              [name] -> Some name
            | _ -> None
    for i=1 to 1120 do
        try
            printfn "Page %d:" i
            let html = getWebPage ("http://www.authorsonline.co.uk/viewauthor.php?pid=" + i.ToString()) None
            let doc = new HtmlDocument()
            doc.LoadHtml html
            let emails = fetch_emails2 doc |> Array.ofSeq
            let author_name = match author_name_of_doc doc with Some x -> x | None -> ""
            emails |> Seq.iter (fun s -> printfn "%s; %s" s author_name)
            emails |> Seq.iter (fun s -> fout.WriteLine(s + "; " + author_name))
        with
            | ex -> printfn "Exception: %s" (ex.ToString() )
        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 2.0)
    fout.Flush()
    printfn "Finished."


// source: http://www.contactanauthor.co.uk/longlist.php
let fetch_web5 () =
    use fout = System.IO.File.CreateText("web5.txt")
    let fetch_email (doc:HtmlDocument) =
        let seq_nodes = doc.DocumentNode.SelectNodes("//span[@class='email']")
        let nodes = if seq_nodes=null then [] else List.ofSeq (seq_nodes)
        let sb = new System.Text.StringBuilder ()
        for n in nodes do
            for cn in n.ChildNodes do
                if sb.Length>0 then
                    sb.Append(cn.InnerText) |> ignore
                else
                    sb.Append(cn.InnerText) |> ignore
                    sb.Append('@') |> ignore
        sb.ToString()
    let fetch_name (doc:HtmlDocument) =
        let seq_nodes = doc.DocumentNode.SelectNodes("//title")
        let nodes = if seq_nodes=null then [] else List.ofSeq (seq_nodes)
        let sb = new System.Text.StringBuilder ()
        for n in nodes do
            sb.Append(n.InnerText) |> ignore
        let x = sb.ToString()
        let mpos = x.IndexOf('-')
        x.Substring(mpos+2)

    for i=601 to 1300 do
        try
            printfn "Page %d:" i
            let html = getWebPage ("http://www.contactanauthor.co.uk/authorpage.php") (Some ("authorname=&talktype=1&audience=1&authortype=1&booktype=1&genre=1&keywords=&postcode=&distance=10000&searchtype2=info&id=" + i.ToString() + "&nextaction=&srctype=searchpage&searchtype=info"))
            // let html = System.IO.File.ReadAllText "x.html"
            let doc = new HtmlDocument()
            doc.LoadHtml html
            let name = fetch_name doc
            if name="helps you find the author you need" then
                printfn "No author with ID of %d" i
            else
                let email = fetch_email doc
                if email.Length>0 then
                    fout.WriteLine(sprintf "%s\t%s" email name)
                    printfn "Name: %s" (name)
                else
                    printfn "Name: %s has no e-mail" (name)
        with
            | ex -> printfn "Exception: %s" (ex.ToString() )
        System.Threading.Thread.Sleep (System.TimeSpan.FromSeconds 10.0)
    fout.Flush()
    printfn "Finished."


// http://www.synerlab.ee/memberlist.php?mode=joined&order=ASC&start=50
#if false
let _ = 
    let html = System.IO.File.ReadAllText("kasutajad.html")
    let urls = fetch_user_urls html "http://foorum.noortele.ee/user/"
    urls |> List.iter (fun s -> printfn "%s" s)
    printfn "Finished."
    ()
#endif
#if false
let _ = 
    let html = System.IO.File.ReadAllText("memberlist.html")
    let emails = fetch_emails html ':' '"'
    emails |> List.iter (fun s -> printfn "%s" s)
    printfn "Finished."
    ()
#endif

fetch_web5 ()
