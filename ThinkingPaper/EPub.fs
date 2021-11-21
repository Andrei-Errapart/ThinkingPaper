namespace ThinkingPaper

open System
open Microsoft.FSharp.Data.TypeProviders
open System.Data.Linq.SqlClient
open System.Linq
open System.Text.RegularExpressions
open Microsoft.FSharp.Linq
open Ionic.Zip
open HtmlAgilityPack

module EPubFunctions =
    type schema = SqlDataConnection<"Data Source=.\SqlExpress;Initial Catalog=Digipuuk;Integrated Security=True">
    let db = schema.GetDataContext()
    // ---------------------------- SETTINGS --------------------------------------
    let template_zip_filename = "EpubTemplate.zip"
    let template_exlibris_filename = "exlibris.html"
    let title_filename = "title.html"
    let coverpage_filename = "coverpage.html"
    let inetpub_directory = "C:\\inetpub"
    let common_files_directory = System.IO.Path.Combine [| inetpub_directory; "CommonFiles" |]
    let cover_image_directory = System.IO.Path.Combine [| common_files_directory; "kaanepilt" |]
    let mimetype_filename = "mimetype"

    // ---------------------------- CONSTANTS ------------------------------------
    let mediatype_image_jpeg = "image/jpeg"
    let mediatype_xhtml = "application/xhtml+xml"

    let html_prefix = "\
        <html>\r\n\
        <head>\r\n\
         <title>CHAPTER_TITLE</title>\r\n\
         <link rel=\"stylesheet\" type=\"text/css\" href=\"Cyrillic.css\"/>\r\n\
        </head>\r\n\
        <body><p>\r\n\
        <font size=\"3\"><b>CHAPTER_TITLE</b></font><br/>&nbsp;<br/>\r\n\
        "

    let xhtml_suffix = "\
        </p></body>\r\n\
        </html>\r\n\
        "

    let toc_ncx_prefix = "\
        <?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n\
        <!DOCTYPE ncx PUBLIC \"-//NISO//DTD ncx 2005-1//EN\" \"http://www.daisy.org/z3986/2005/ncx-2005-1.dtd\">\r\n\
        <ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" xml:lang=\"en\" version=\"2005-1\">\r\n\
	        <head>\r\n\
		        <meta name=\"dtb:uid\" content=\"BOOK_ID\" />\r\n\
		        <meta name=\"dtb:depth\" content=\"1\" />\r\n\
		        <meta name=\"dtb:totalPageCount\" content=\"0\" />\r\n\
		        <meta name=\"dtb:maxPageNumber\" content=\"0\" />\r\n\
	        </head>\r\n\
	        <docTitle>\r\n\
		        <text>TITLE</text>\r\n\
	        </docTitle>\r\n\
    	    <navMap>\r\n\
          <navPoint id=\"navPoint-0\" playOrder=\"0\">\r\n\
             <navLabel>\r\n\
                <text>Title Page</text>\r\n\
             </navLabel>\r\n\
             <content src=\"title.html\"/>\r\n\
          </navPoint>\r\n\
          "

    let toc_ncx_suffix = "\
	        </navMap>\r\n\
        </ncx>\r\n\
        "

    let toc_ncx_entry (index: int) (title: string) =
        sprintf "<navPoint id=\"navpoint-%d\" playOrder=\"%d\">\r\n\
	        <navLabel>\r\n\
		        <text>%s</text>\r\n\
	        </navLabel>\r\n\
	        <content src=\"%03d.html\" />\r\n\
        </navPoint>\r\n" index index title index

    let content_opf_prefix = "\
        <?xml version=\"1.0\"  encoding=\"UTF-8\"?>\r\n\
        <package xmlns=\"http://www.idpf.org/2007/opf\" version=\"2.0\" unique-identifier=\"dcidid\">\r\n\
	        <metadata\r\n\
                xmlns:dc=\"http://purl.org/dc/elements/1.1/\"\r\n\
                xmlns:opf=\"http://www.idpf.org/2007/opf\"\r\n\
                xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" >\r\n\
		        <dc:creator opf:role=\"aut\" opf:file-as=\"AUTHOR_LASTNAME, AUTHOR_FIRSTNAME\">AUTHOR</dc:creator>\r\n\
                <dc:title>TITLE</dc:title>\r\n\
		        <dc:date xsi:type=\"dcterms:W3CDTF\">DATETIME_NOW</dc:date>\r\n\
		        <dc:language>en-GB</dc:language>\r\n\
		        <dc:publisher>E-raamatukogu.com</dc:publisher>\r\n\
		        <dc:identifier id=\"dcidid\" opf:scheme=\"URI\">BOOK_ID</dc:identifier>\r\n\
		        <dc:description>DESCRIPTION</dc:description>\r\n\
		        <meta name=\"cover\" content=\"coverPic\"/>\r\n\
	        <dc:subject>CATEGORY</dc:subject>\r\n\
        </metadata>\r\n\
	        <manifest>\r\n\
		        <item href=\"fonts/LiberationSerif-Regular.ttf\" media-type=\"application/octet-stream\" id=\"font-1\"/>\r\n\
		        <item href=\"fonts/LiberationSerif-Italic.ttf\" media-type=\"application/octet-stream\" id=\"font-2\"/>\r\n\
		        <item href=\"fonts/LiberationSerif-BoldItalic.ttf\" media-type=\"application/octet-stream\" id=\"font-3\"/>\r\n\
		        <item href=\"fonts/LiberationSerif-Bold.ttf\" media-type=\"application/octet-stream\" id=\"font-4\"/>\r\n\
		        <item id=\"ncx\"                href=\"toc.ncx\"                media-type=\"application/x-dtbncx+xml\"/>\r\n\
                <item id=\"css\"                href=\"Cyrillic.css\"           media-type=\"text/css\" />\r\n\
		        <item id=\"coverpage\"          href=\"coverpage.html\"         media-type=\"application/xhtml+xml\"/>\r\n\
                <item id=\"exlibris\"           href=\"exlibris.html\"          media-type=\"application/xhtml+xml\" />\r\n\
                <item id=\"title\"              href=\"title.html\"             media-type=\"application/xhtml+xml\" />\r\n\
                <item id=\"image-exlibris\"     href=\"images/exlibris.jpg\"    media-type=\"image/jpeg\" />\r\n\
                <item id=\"image-coverpage\"    href=\"coverpage.jpg\"          media-type=\"image/jpeg\" />\r\n\
                "
                // items for images and chapters

    let content_opf_item (id: string) (filename: string) (mediatype:string) =
        sprintf "<item id=\"%s\" href=\"%s\" media-type=\"%s\"/>\r\n" id filename mediatype

    let content_opf_middle = "\
	    </manifest>\r\n\
	    <spine toc=\"ncx\">\r\n\
            <itemref idref=\"coverpage\"/>\r\n\
            <itemref idref=\"exlibris\"/>\r\n\
            <itemref idref=\"title\"/>\r\n\
            "

    let content_opf_itemref (id: string) =
        sprintf "<itemref idref=\"%s\"/>\r\n" id

    let content_opf_suffix = "\
	    </spine>\r\n\
	    <guide>\r\n\
		    <reference href=\"coverpage.html\" type=\"cover\" title=\"Cover\"/>\r\n\
	    </guide>\r\n\
    </package>\r\n
    "

    // ---------------------------- HELPER FUNCTIONS ------------------------------
    let filename_of_title (s: string) =
        s.Replace(' ', '_')
            .Replace('/', '-')
            .Replace('/', '-')
            .Replace("\"", "")
            .Replace(':', '-') + ".epub"

    let filename_in_server (path_in_url: string) =
        let s = path_in_url.Replace('/', '\\')
        if s.[0] = '~' && s.[1]='\\' then System.IO.Path.Combine [| common_files_directory; s.Substring 2 |] else s


    let memory_stream_of_string (s:string) =
        let bytes = Array.concat [ [| 0xEFuy; 0xBBuy; 0xBFuy |]; System.Text.Encoding.UTF8.GetBytes(s) ]
        // let bytes = System.Text.Encoding.UTF8.GetBytes(s)
        new System.IO.MemoryStream(bytes, false)

    let _image_file_types = [
        ([| 0x89uy; 0x50uy; 0x4Euy; 0x47uy |], "png", "image/png");
        ([| 0xFFuy; 0xD8uy |], "jpg", "image/jpeg");
        ([| 0x47uy; 0x49uy; 0x46uy; 0x38uy; 0x39uy |], "gif", "image/gif")
        ]

    let correct_file_extension (filename : string)  (stream : System.IO.Stream) =
        let b = Array.create 16 0uy
        stream.Read(b, 0, b.Length) |> ignore
        stream.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        let is_magic_match ((m:byte array), _, _) =
            let mutable r = true
            for i=0 to m.Length-1 do
                r <- r && (m.[i] = b.[i])
            r
        let magic, extension, mimetype = List.find is_magic_match _image_file_types
        let new_filename = System.IO.Path.ChangeExtension(filename,extension)
        new_filename

    let mimetype_of_filename (filename:string) =
        let extension = (System.IO.Path.GetExtension filename).ToLower().Substring(1)
        let _, _, mimetype = List.find (fun (_,ext,mimetype) -> ext=extension) _image_file_types
        mimetype

    let clean_html (html: string) =
        let custom_html_clean (begin_str: string) (end_str: string) (html: string) =
            let mutable r = html
            let mutable found = true
            while found do
                let i = r.IndexOf begin_str
                let j = r.IndexOf end_str
                found <- i>=0 && j>=0
                if found then
                    r <- r.Remove(i, ((j - i) + end_str.Length))
            r
        let html2 = Regex.Replace(html, "<[/]?(font|link|m|st1|meta|object|style|span|xml|del|ins|[ovwxp]:\w+)[^>]*?>", "", RegexOptions.IgnoreCase)
    #if BLAH_BLAH
        let rx_align = new Regex("<div\s+style=\"\s*text-align:\s*\w+\s*;\s*\"\s*>", RegexOptions.IgnoreCase)
        let mx = Regex.Matches(html2, "<([^>]*)(?:class|lang|style|size|face|[ovwxp]:\w+)=(?:'[^']*'|\"[^\"]*\"|[^\s>]+)([^>]*)>", RegexOptions.IgnoreCase)
        let sb = new System.Text.StringBuilder()
        let mutable so_far = 0
        for m in mx do
            // text before us
            sb.Append(html2.Substring(so_far, m.Index - so_far)) |> ignore
            so_far <- m.Index + m.Length
            // Is it alignment?
            sb.Append(if rx_align.IsMatch(m.Value) then m.Value else ("<" + m.Groups.[1].ToString() + m.Groups.[2].ToString() + ">")) |> ignore
        if mx.Count > 0 then
            let m = mx.[mx.Count - 1]
            sb.Append(html2.Substring(m.Index + m.Length)) |> ignore
    #endif
        html2 |> custom_html_clean "<!--[if" "<![endif]-->" |> custom_html_clean "<!-- /*" "-->"

    let xhtml_of_html (html: string) =
        let utf8 = System.Text.Encoding.UTF8
        let ps_info = new System.Diagnostics.ProcessStartInfo("html2xhtml.exe", "")
        ps_info.UseShellExecute <- false
        ps_info.CreateNoWindow <- true
        ps_info.RedirectStandardInput <- true
        ps_info.RedirectStandardOutput <- true
        ps_info.StandardOutputEncoding <- utf8
        ps_info.Arguments <- "--ics utf-8 --ocs utf-8"
        let ps = System.Diagnostics.Process.Start(ps_info)
        let bytes = utf8.GetBytes(html)
        ps.StandardInput.BaseStream.Write(bytes, 0, bytes.Length)
        ps.StandardInput.Close()
        let r = ps.StandardOutput.ReadToEnd()
        r

    let accepted_chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.() "
    let escape_filename (filename:string) =
        let sb = new System.Text.StringBuilder()
        for ch in filename do
            if accepted_chars.IndexOf(ch) >= 0 then
                sb.Append ch |> ignore
            else
                sb.Append(sprintf "%d" (int ch)) |> ignore
        sb.ToString()

    let add_image (epub : ZipFile) (content_opf: System.Text.StringBuilder) (folder_name:string) (escaped_filename:string) (file:System.IO.Stream) =
        let full_filename = (if folder_name.Length=0 then "" else (folder_name + "/")) + escaped_filename
        let mutable already_added = false
        for e in epub.Entries do
            already_added <- already_added || e.FileName=full_filename
        if not already_added then
            epub.AddEntry(full_filename, file) |> ignore
            let id = "image-" + (if folder_name.Length=0 then "" else (folder_name + "-")) + (System.IO.Path.GetFileNameWithoutExtension escaped_filename)
            content_opf.Append (content_opf_item id  full_filename (mimetype_of_filename full_filename)) |> ignore
        ()

    let image_folders = [
        "wwwroot\\kaanepilt";
        "wwwroot\\Illustratsioonid";
        "CommonFiles\\kaanepilt";
        "CommonFiles\\kaanepilt\\banners";
        "CommonFiles\\kaanepilt\\bellapildid";
        "CommonFiles\\kaanepilt\\Illustratsioonid";
        "CommonFiles\\kaanepilt\\Raamatukaas";
        "CommonFiles\\kaanepilt\\Illustratsioonid";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\aidi_vallik_koeraraamat";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\doctor_doolitle";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\janku";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\kivid";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\pallo_kurbuseta_elu";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\poe_ronk";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\tuhkatriinu";
        "CommonFiles\\kaanepilt\\Illustratsioonid\\under_the_sea";
        "wwwroot\\icons";
        ]

    let find_image_in_server (author_id:System.Guid) (filename:string) =
        let possible_filenames = seq {
            for subdir in (sprintf "CommonFiles\\images\\%s" (author_id.ToString())) :: image_folders do
                let full_filename = System.IO.Path.Combine(System.IO.Path.Combine(inetpub_directory, subdir), filename)
                yield full_filename
        }
        try
            Some (Seq.find System.IO.File.Exists possible_filenames)
        with
            | ex -> None

    let memorystream_of_file (filename:string) =
        use file = System.IO.File.Open(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read)
        let n = int file.Length
        let bytes = Array.create n 0uy
        file.Read(bytes, 0, bytes.Length) |> ignore
        let r = new System.IO.MemoryStream(bytes)
        r

    /// Fix HTML links and remove "style" attributes from paragraphs.
    let fix_html_links (author_id:System.Guid) (html: string) (make_image_filename:unit -> string) =
        let doc = new HtmlDocument()
        doc.LoadHtml html
        let p_nodes = doc.DocumentNode.SelectNodes("//p[@style]")
        if p_nodes<>null then
            for n in p_nodes do
                n.Attributes.Remove("style")
        let img_nodes =
            let nodes =
                let seq_nodes = doc.DocumentNode.SelectNodes("//img[@src]")
                if seq_nodes=null then [] else List.ofSeq (seq_nodes)
            let r = seq {
                for n in nodes do
                    let src_tag =  n.GetAttributeValue("src", "")
                    if src_tag.StartsWith "data:" then
                        // example: data:image/png;base64,iVBORw0KGgoAA.....
                        let semicolon_pos, comma_pos = src_tag.IndexOf ';', src_tag.IndexOf ','
                        let mimetype_field = src_tag.Substring(5, semicolon_pos-5)
                        let encoding_field = src_tag.Substring(semicolon_pos+1, comma_pos - semicolon_pos-1)
                        let data_field = src_tag.Substring(comma_pos+1)
                        let bytes = System.Convert.FromBase64String data_field
                        let file = new System.IO.MemoryStream(bytes)
                        let escaped_filename = correct_file_extension (make_image_filename ()) file
                        n.SetAttributeValue("src", "images/" + escaped_filename) |> ignore
                        yield (file, escaped_filename)
                    else
                        let filename = src_tag |> System.Web.HttpUtility.HtmlDecode |> System.Web.HttpUtility.UrlDecode |> System.IO.Path.GetFileName
                        let m = find_image_in_server author_id filename
                        if m.IsSome then
                            let file = memorystream_of_file m.Value
                            let escaped_filename = correct_file_extension (escape_filename filename) file
                            n.SetAttributeValue("src", "images/" + escaped_filename) |> ignore
                            yield (file, escaped_filename)
                        else
                            printfn "File not found: %s" filename
                        (*
                        let fs_filename = filename_in_server ("~/kaanepilt/" + filename)
                        if System.IO.File.Exists fs_filename then
                            let file = System.IO.File.Open(filename_in_server ("~/kaanepilt/" + filename), System.IO.FileMode.Open, System.IO.FileAccess.Read) :> System.IO.Stream
                            let escaped_filename = correct_file_extension (escape_filename filename) file
                            n.SetAttributeValue("src", "images/" + escaped_filename) |> ignore
                            yield (file, escaped_filename)
                        else
                            printfn "File not found: %s" fs_filename
                        *)
            }
            List.ofSeq r
        use ms = new System.IO.MemoryStream()
        // ms.Write([| 0xEFuy; 0xBBuy; 0xBFuy |], 0, 3)
        doc.Save(ms, System.Text.Encoding.UTF8)
        ms.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
        use tr = new System.IO.StreamReader(ms)
        img_nodes, tr.ReadToEnd()

    let memorystream_of_zipentry (ze:ZipEntry) =
        let m = new System.IO.MemoryStream(Array.create ((int)ze.UncompressedSize) ((byte)0), true)
        ze.Extract m
        m.Seek(0L, IO.SeekOrigin.Begin) |> ignore
        m

    // ---------------------------------------------------------------------------
    let make_epub_by_zip (zip_template: ZipFile) (rid: int) (uid: Guid) (output_directory: string) (website: string) = 
        let aspnet_user = query { for u in db.Aspnet_Users do where (u.UserId = uid); select u; exactlyOne }
        let aspnet_membership = query { for u in db.Aspnet_Membership do where (u.UserId = uid); select u; exactlyOne }
        let book = query { for p in db.Raamatud do where (p.RID=rid); select p; exactlyOne}
        let author_id = book.UserId
        let chapters =
            let raw_chapters = (query { for ch in db.Alaraamat do where (ch.RID = rid); sortBy ch.AlaraamatID; select ch }).ToArray()
            Array.zip [| 1 .. raw_chapters.Length |] raw_chapters
        let book_id = sprintf "%s/epub.aspx?rid=%d&amp;uid=%s" website rid (uid.ToString())

        use epub = new ZipFile()
        let output_filename =
            let title_as_filename = filename_of_title book.Pealkiri
            System.IO.Path.Combine(output_directory, title_as_filename)
        // printfn "Output filename: '%s'." output_filename;
        let coverimage_file, coverimage_filename = 
            let fn = filename_in_server book.Pilt
            if System.IO.File.Exists fn then
                let file = memorystream_of_file fn
                file, correct_file_extension "coverpage.jpg" file
            else
                new System.IO.MemoryStream(), ""

        let processed_template (ts:System.IO.MemoryStream) =
            use tr = new System.IO.StreamReader(ts)
            let template = tr.ReadToEnd()
            ts.Close()
            let processed_template = template.Replace("DATETIME_NOW", DateTime.Now.ToString("yyyy-MM-dd"))
                                        .Replace("USER_NAME", aspnet_user.UserName)
                                        .Replace("USER_EMAIL", aspnet_membership.Email)
                                        .Replace("USER_ID", uid.ToString())
                                        .Replace("AUTHOR", book.Eesnimi + " " + book.Perekonnanimi)
                                        .Replace("DESCRIPTION", book.Sisututvustus)
                                        .Replace("TITLE", book.Pealkiri)
                                        .Replace("BOOK_ID", book_id)
                                        .Replace("COVERIMAGE_FILENAME", coverimage_filename)
            memory_stream_of_string processed_template

        // 1. Copy the template over
        // 1a. First the "mimetype" file.
        let source_mimetype_entry = query { for mz in zip_template.Entries do where (mz.FileName=mimetype_filename); select mz; exactlyOne}
        let mimetype_entry = epub.AddEntry(mimetype_filename, memorystream_of_zipentry source_mimetype_entry)
        mimetype_entry.CompressionMethod <- CompressionMethod.None
        // 2a. Then, the remaining.
        for ze in query { for mz in zip_template.Entries do where (mz.FileName<>mimetype_filename); select mz } do
            // printfn "Template file: %s" ze.FileName
            let ms =
                let m = memorystream_of_zipentry ze
                if ze.FileName=template_exlibris_filename then
                    // social exlibris stuff.
                    processed_template m
                elif ze.FileName=title_filename then
                    // description page.
                    processed_template m
                elif ze.FileName=coverpage_filename then
                    processed_template m
                else
                    m
            epub.AddEntry(ze.FileName, ms) |> ignore

        // 2. Copy the cover page.
        let sb_items_chapter_images = new System.Text.StringBuilder()
        if coverimage_filename.Length>0 then
            add_image epub sb_items_chapter_images "" coverimage_filename coverimage_file

        let make_image_counter = ref 0
        let make_image_filename () =
            make_image_counter := !make_image_counter + 1
            sprintf "embedded-in-html-%03d.jpg" !make_image_counter
        // 3. Copy the chapters, incl. images
        for i,ch in chapters do
            // printfn "Chapter %d: %s" i ch.Alapealkiri
            let image_filenames, html = fix_html_links author_id (html_prefix.Replace("CHAPTER_TITLE", (System.Web.HttpUtility.HtmlEncode ch.Alapealkiri)) + (clean_html ch.Sisu) + xhtml_suffix) make_image_filename
            let html2 = Regex.Replace(html, @"<br */?>", "</p><p>", RegexOptions.IgnoreCase)
            // let s = "</p><p>"
            // html.Replace("<br/>", s).Replace("<br />", s).Replace("<br>", s)
            let xhtml = xhtml_of_html html2
            epub.AddEntry(sprintf "%03d.html" i, (memory_stream_of_string xhtml)) |> ignore
            for file, escaped_filename in image_filenames do
                // printfn "Image: %s" fn
                add_image epub sb_items_chapter_images "images" escaped_filename file
        let items_chapter_images = sb_items_chapter_images.ToString()

        // 4. Create the "doc.ncx" file.
        let doc_ncx =
            let s_chapters =
                chapters
                |> Array.map (fun (i, ch) -> toc_ncx_entry i ch.Alapealkiri)
                |> String.concat ""
            toc_ncx_prefix.Replace("TITLE", book.Pealkiri).Replace("BOOK_ID", book_id)
                + s_chapters
                + toc_ncx_suffix
        epub.AddEntry("toc.ncx", memory_stream_of_string doc_ncx) |> ignore

        // 5. Create the "content.opf" file.
        let content_opf =
            let prefix = content_opf_prefix
                                .Replace("AUTHOR_LASTNAME", book.Perekonnanimi)
                                .Replace("AUTHOR_FIRSTNAME", book.Eesnimi)
                                .Replace("AUTHOR", book.Eesnimi + " " + book.Perekonnanimi)
                                .Replace("BOOK_ID", book_id)
                                .Replace("DESCRIPTION", book.Sisututvustus)
                                .Replace("CATEGORY", book.Kategooriad.Kategooria)
                                .Replace("TITLE", book.Pealkiri)
                                .Replace("DATETIME_NOW", DateTime.Now.ToString("yyyy-MM-dd"))
            let items_chapters =
                chapters
                |> Array.map (fun (i,ch) -> content_opf_item (sprintf "ch%03d" i) (sprintf "%03d.html" i) mediatype_xhtml)
                |> String.concat ""
            let itemrefs = chapters |> Array.map (fun (i, ch) -> content_opf_itemref (sprintf "ch%03d" i)) |> String.concat ""
            prefix
                + items_chapters + items_chapter_images
                + content_opf_middle
                + itemrefs
                + content_opf_suffix
        epub.AddEntry("content.opf", memory_stream_of_string content_opf) |> ignore

        // Finish!
        output_filename, epub

type EPub =
    val Filename : string
    val EPub : ZipFile
    new (template: ZipFile, rid:int, uid:Guid, output_directory:string, website:string) =
        let f, e = EPubFunctions.make_epub_by_zip template rid uid output_directory website
        { Filename = f; EPub=e; }
    new (template_filename: string, rid:int, uid:Guid, output_directory:string, website:string) =
        let f, e = EPubFunctions.make_epub_by_zip (ZipFile.Read template_filename) rid uid output_directory website
        { Filename = f; EPub=e; }
    member this.Save(outStream: System.IO.Stream) =
        this.EPub.Save(outStream)
    member this.Save(outFilename: string) =
        this.EPub.Save(outFilename)
