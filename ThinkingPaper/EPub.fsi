namespace ThinkingPaper

type EPub =
    class
        /// Name of the EPub file.
        val Filename : string
        /// ZIP file corresponding to EPub.
        val EPub : Ionic.Zip.ZipFile

        /// Template, RID, User ID, Output_directory, Website
        new : Ionic.Zip.ZipFile * int * System.Guid * string * string -> EPub
        /// Template, RID, User ID, Output_directory, Website
        new : string * int * System.Guid * string * string -> EPub

        member Save : System.IO.Stream -> unit
    end