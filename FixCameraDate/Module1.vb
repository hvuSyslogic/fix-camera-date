﻿Option Strict On
Imports System.Net.Http
Imports System.Threading
Imports <xmlns="http://schemas.microsoft.com/search/local/ws/rest/v1">


Module Module1

    Sub TestMetadata()
        Dim fns = IO.Directory.GetFiles("test")
        For Each fn In fns
            Dim ft = FilestampTime(fn)
            Dim mt = MetadataTimeAndGps(fn)
            Console.WriteLine($"{fn}{vbCrLf}    ft={ft?.Item1}{vbCrLf}    mt={mt?.Item1}{vbCrLf}    gps={mt?.Item3}")
        Next
    End Sub

    Sub TestGps()
        'Sub DoGps(gpsToDo0 As Dictionary(Of Integer, FileToDo), filesToDo As Queue(Of FileToDo))
        Dim gpsToDo As New Dictionary(Of Integer, FileToDo)
        Dim filesToDo As New Queue(Of FileToDo)
        gpsToDo.Add(11, New FileToDo With {.fn = "Montlake", .gpsCoordinates = New GpsCoordinates(47.637922, -122.301557)})
        gpsToDo.Add(12, New FileToDo With {.fn = "Volunteer Park", .gpsCoordinates = New GpsCoordinates(47.629612, -122.315119)})
        gpsToDo.Add(13, New FileToDo With {.fn = "Arboretum", .gpsCoordinates = New GpsCoordinates(47.639483, -122.29801)})
        gpsToDo.Add(14, New FileToDo With {.fn = "Husky Stadium", .gpsCoordinates = New GpsCoordinates(47.65076, -122.302043)})
        gpsToDo.Add(15, New FileToDo With {.fn = "UW Campus", .gpsCoordinates = New GpsCoordinates(47.656849, -122.309596)})
        gpsToDo.Add(16, New FileToDo With {.fn = "Ballard", .gpsCoordinates = New GpsCoordinates(47.668719, -122.38296)})
        gpsToDo.Add(17, New FileToDo With {.fn = "Shilshole", .gpsCoordinates = New GpsCoordinates(47.681006, -122.407513)})
        gpsToDo.Add(18, New FileToDo With {.fn = "Space needle", .gpsCoordinates = New GpsCoordinates(47.620415, -122.349463)})
        gpsToDo.Add(19, New FileToDo With {.fn = "Pike Place Market", .gpsCoordinates = New GpsCoordinates(47.609839, -122.342981)})
        gpsToDo.Add(20, New FileToDo With {.fn = "Chaplains' Court in Old Aberdeen, Scotland", .gpsCoordinates = New GpsCoordinates(57.169365, -2.101216)})
        gpsToDo.Add(21, New FileToDo With {.fn = "Aberdeen", .gpsCoordinates = New GpsCoordinates(57.14727, -2.095665)})
        gpsToDo.Add(22, New FileToDo With {.fn = "Lihue", .gpsCoordinates = New GpsCoordinates(21.97472, -159.3656)})
        gpsToDo.Add(23, New FileToDo With {.fn = "Barking Sands beach in Hawaii", .gpsCoordinates = New GpsCoordinates(22.034308, -159.785638)})
        gpsToDo.Add(24, New FileToDo With {.fn = "Darra, Brisbane", .gpsCoordinates = New GpsCoordinates(-27.5014, 152.97272)})
        gpsToDo.Add(25, New FileToDo With {.fn = "Queens' College Cambridge", .gpsCoordinates = New GpsCoordinates(52.196766, 0.12255)})
        DoGps(gpsToDo, filesToDo)
        For Each file In filesToDo
            Console.WriteLine($"{file.fn} ... {file.gpsResult}")
        Next
    End Sub

    Sub Main(args As String())
        TestGps() : Return
        ' Goals:
        ' (1) If you have shots from one or more devices, rename them to local time when the shot was taken
        ' (2) If you had taken shots on holiday without having fixed the timezone on your camera, fix their metadata timestamps
        ' also...
        ' (3) Just display the time from a given shot

        ' METADATA DATES:
        ' JPEG:       metadata dates are "unspecified", i.e. local to the time the photo was taken, but without saying which timezone that was
        ' MOV-iphone: metadata dates are "local", i.e. local to the time the video was taken, and they do say which timezone
        ' MOV-canon:  metadata dates officially are "UTC", but we can also find undocumented the "unspecified" version
        ' MP4-sony:   metadata dates claim to be UTC, but in reality they're unspecified, i.e. local to the time the video was taken, and we don't have information about what timezone
        ' MP4-android:metadata dates are in UTC as they claim, but have the "year" 66 years in the past
        ' MP4-wp8:    metadata dates are absent.
        '
        ' FILESTAMP DATES:
        ' In all cases, filestamp dates are UTC.
        ' In some cases they're the filestamp from when the photo/video was taken and written immediately to SD card
        ' In other cases they're the filestamp from when the photo/video was uploaded to Skydrive
        ' In other cases they're the filestamp from when the photo/video was last copied to the folder

        ' Our objective is to rename the file according to "local" time when the photo/video was taken, without the timezone information.

        Dim cmd = ParseCommandLine(args)


        For Each cmdFn In cmd.Fns
            Dim fileToDo As New FileToDo With {.fn = cmdFn}

            Dim mtt = MetadataTimeAndGps(fileToDo.fn)
            Dim ftt = FilestampTime(fileToDo.fn)
            Dim gps As GpsCoordinates = Nothing
            If mtt Is Nothing Then Console.WriteLine("Not an image/video - ""{0}""", IO.Path.GetFileName(fileToDo.fn)) : Continue For
            Dim mt = mtt.Item1, ft = ftt.Item1
            If mt.HasValue Then
                fileToDo.setter = mtt.Item2
                gps = mtt.Item3
                If mt.Value.dt.Kind = System.DateTimeKind.Unspecified OrElse mt.Value.dt.Kind = System.DateTimeKind.Local Then
                    ' If dt.kind=Unspecified (e.g. EXIF, Sony), then the time is by assumption already local from when the picture was shot
                    ' If dt.kind=Local (e.g. iPhone-MOV), then the time is local, and also indicates its timezone offset
                    fileToDo.localTime = mt.Value.dt
                ElseIf mt.Value.dt.Kind = System.DateTimeKind.Utc Then
                    ' If dt.Kind=UTC (e.g. Android), then time is in UTC, and we don't know how to read timezone.
                    fileToDo.localTime = mt.Value.dt.ToLocalTime() ' Best we can do is guess the timezone of the computer
                End If
            Else
                fileToDo.setter = ftt.Item2
                If ft.dt.Kind = System.DateTimeKind.Unspecified Then
                    ' e.g. Windows Phone when we got the date from the filename
                    fileToDo.localTime = ft.dt
                ElseIf ft.dt.Kind = System.DateTimeKind.Utc Then
                    ' e.g. all other files where we got the date from the filestamp
                    fileToDo.localTime = ft.dt.ToLocalTime() ' the best we can do is guess that the photo was taken in the timezone as this computer now
                Else
                    Throw New Exception("Expected filetimes To be In UTC")
                End If
            End If


            ' The only thing that requires GPS is if (1) we're doing a rename, (2) the
            ' pattern includes place, (3) the file actually has a GPS signature
            If cmd.Pattern.Contains("%{place}") AndAlso gps IsNot Nothing Then
                fileToDo.place = OpenStreetMapSearch(gps.Latitude, gps.Longitude)
            End If


            If cmd.Pattern = "" AndAlso Not cmd.Offset.HasValue Then
                Console.WriteLine("""{0}"": {1:yyyy.MM.dd - HH.mm.ss}", IO.Path.GetFileName(fileToDo.fn), fileToDo.localTime)
            End If


            If cmd.Offset.HasValue Then
                Using file = New IO.FileStream(fileToDo.fn, IO.FileMode.Open, IO.FileAccess.ReadWrite)
                    Dim prevTime = fileToDo.localTime
                    Dim r = fileToDo.setter(file, cmd.Offset.Value)
                    If r Then
                        fileToDo.localTime += cmd.Offset.Value
                        If cmd.Pattern = "" Then Console.WriteLine("""{0}"": {1:yyyy.MM.dd - HH.mm.ss}, corrected from {2:yyyy.MM.dd - HH.mm.ss}", IO.Path.GetFileName(fileToDo.fn), fileToDo.localTime, prevTime)
                    End If
                End Using
            End If


            If cmd.Pattern <> "" Then
                ' Attempt to match the existing filename against the pattern
                Dim basefn = IO.Path.GetFileNameWithoutExtension(fileToDo.fn)
                Dim matchremainder = basefn
                Dim matchParts As New LinkedList(Of PatternPart)(cmd.PatternParts)
                Dim matchExt = If(cmd.PatternExt, IO.Path.GetExtension(fileToDo.fn))

                While matchParts.Count > 0 AndAlso matchremainder.Length > 0
                    Dim matchPart As PatternPart = matchParts.First.Value
                    Dim matchLength = matchPart.matcher(matchremainder)
                    If matchLength = -1 Then Exit While
                    matchParts.RemoveFirst()
                    If matchPart.pattern = "%{fn}" Then basefn = matchremainder.Substring(0, matchLength)
                    matchremainder = matchremainder.Substring(matchLength)
                End While

                If matchremainder.Length = 0 AndAlso matchParts.Count = 2 AndAlso matchParts(0).pattern = " - " AndAlso matchParts(1).pattern = "%{place}" Then
                    ' hack if you had pattern like "%{year} - %{fn} - %{place}" so
                    ' it will match a filename like "2012 - file.jpg" which lacks a place
                    matchParts.Clear()
                End If

                If matchParts.Count <> 0 OrElse matchremainder.Length > 0 Then
                    ' failed to do a complete match
                    basefn = IO.Path.GetFileNameWithoutExtension(fileToDo.fn)
                End If
                '
                ' 4. Figure out the new filename
                Dim newfn = IO.Path.GetDirectoryName(fileToDo.fn) & "\"
                For Each patternPart In cmd.PatternParts
                    newfn &= patternPart.generator(basefn, fileToDo.localTime, fileToDo.place)
                Next
                If cmd.PatternParts.Count > 2 AndAlso cmd.PatternParts.Last.Value.pattern = "%{place}" AndAlso patternParts.Last.Previous.Value.pattern = " - " AndAlso String.IsNullOrEmpty(fileToDo.gpsResult) Then
                    If newfn.EndsWith(" - ") Then newfn = newfn.Substring(0, newfn.Length - 3)
                End If
                newfn &= matchExt
                If fileToDo.fn <> newfn Then
                    If IO.File.Exists(newfn) Then Console.WriteLine("Already exists - " & IO.Path.GetFileName(newfn)) : Continue While
                    Console.WriteLine(IO.Path.GetFileName(newfn))
                    IO.File.Move(fileToDo.fn, newfn)
                End If
            End If

        Next
    End Sub

    Function ParseCommandLine(args As String()) As Commandline
        Dim cmdArgs As New LinkedList(Of String)(args)
        Dim r As New Commandline

        While cmdArgs.Count > 0
            Dim cmd = cmdArgs.First.Value : cmdArgs.RemoveFirst()

            If cmd = "-rename" OrElse cmd = "/rename" Then
                If r.Pattern <> "" Then Console.WriteLine("duplicate /rename") : Return Nothing
                r.Pattern = "%{datetime} - %{fn} - %{place}"
                If cmdArgs.Count > 0 AndAlso Not cmdArgs.First.Value.StartsWith("/") AndAlso Not cmdArgs.First.Value.StartsWith("-") Then r.Pattern = cmdArgs.First.Value : cmdArgs.RemoveFirst()

            ElseIf cmd.StartsWith("/day") OrElse cmd.StartsWith("/hour") OrElse cmd.StartsWith("/minute") OrElse
                                cmd.StartsWith("-day") OrElse cmd.StartsWith("-hour") OrElse cmd.StartsWith("-minute") Then
                Dim len = 0, mkts As Func(Of Integer, TimeSpan) = Function() Nothing
                If cmd.StartsWith("/day") OrElse cmd.StartsWith("-day") Then len = 4 : mkts = Function(n) TimeSpan.FromDays(n)
                If cmd.StartsWith("/hour") OrElse cmd.StartsWith("-hour") Then len = 5 : mkts = Function(n) TimeSpan.FromHours(n)
                If cmd.StartsWith("/minute") OrElse cmd.StartsWith("-minute") Then len = 7 : mkts = Function(n) TimeSpan.FromMinutes(n)
                Dim snum = cmd.Substring(len)
                If Not snum.StartsWith("+") AndAlso Not snum.StartsWith("-") Then Console.WriteLine(cmd) : Return Nothing
                Dim num = 0 : If Not Integer.TryParse(snum, num) Then Console.WriteLine(cmd) : Return Nothing
                r.Offset = If(r.Offset.HasValue, r.Offset, Nothing)
                r.Offset = r.Offset + mkts(num)

            ElseIf cmd = "/?" OrElse cmd = "/help" OrElse cmd = "-help" OrElse cmd = "--help" Then
                r.Fns.Clear() : Exit While

            ElseIf cmd.StartsWith("-") Then
                ' unknown option, so error out
                Console.WriteLine(cmd) : Return Nothing
                ' We'd also like to error out on unknown options that start with "/",
                ' but can't, because that's a valid filename in unix.

            Else
                If cmd.Contains("*") OrElse cmd.Contains("?") Then
                    Dim globPath = IO.Path.GetDirectoryName(cmd), globMatch = IO.Path.GetFileName(cmd)
                    If globPath.Contains("*") OrElse globPath.Contains("?") Then Console.WriteLine("Can't match wildcard directory names") : Return Nothing
                    If globPath = "" Then globPath = IO.Directory.GetCurrentDirectory()
                    Dim fns = IO.Directory.GetFiles(globPath, globMatch)
                    If fns.Length = 0 Then Console.WriteLine($"Not found - ""{cmd}""")
                    r.Fns.AddRange(fns)
                Else
                    If IO.File.Exists(cmd) Then
                        r.Fns.Add(cmd)
                    Else
                        Console.WriteLine($"Not found - ""{cmd}""")
                    End If
                End If
            End If
        End While

        If r.Fns.Count = 0 Then
            Console.WriteLine("FixCameraDate ""a.jpg"" ""b.jpg"" [-rename [""pattern""]] [-day+n] [-hour+n] [-minute+n]")
            Console.WriteLine("  Filename can include * and ? wildcards")
            Console.WriteLine("  -rename: pattern defaults to ""%{datetime} - %{fn} - %{place}"" and")
            Console.WriteLine("           can include %{datetime,fn,date,time,year,month,day,hour,minute,second,place}")
            Console.WriteLine("  -day,-hour,-minute: adjust the timestamp; can be + or -")
            Console.WriteLine()
            Console.WriteLine("EXAMPLES:")
            Console.WriteLine("FixCameraDate ""a.jpg""")
            Console.WriteLine("FixCameraDate ""*.jpg"" -rename ""%{date} - %{time} - %{fn}.jpg""")
            Console.WriteLine("FixCameraDate ""\files\*D*.mov"" -hour+8 -rename")
            Return Nothing
        End If


        ' Filename heuristics:
        ' (1) If the user omitted an extension from the rename string, then we re-use the one that was given to us
        ' (2) If the filename already matched our datetime format, then we figure out what was the base filename
        If Not r.Pattern.Contains("%{fn}") Then Console.WriteLine("Please include %{fn} in the pattern") : Return Nothing
        If r.Pattern.Contains("\") Then Console.WriteLine("Folders not allowed in pattern") : Return Nothing
        If r.Pattern.Split({"%{fn}"}, StringSplitOptions.None).Length <> 2 Then Console.WriteLine("Please include %{fn} only once in the pattern") : Return Nothing
        '
        ' 1. Extract out the extension
        Dim pattern = r.Pattern
        r.PatternExt = Nothing
        For Each potentialExt In {".jpg", ".mp4", ".mov", ".jpeg"}
            If Not pattern.ToLower.EndsWith(potentialExt) Then Continue For
            r.PatternExt = pattern.Substring(pattern.Length - potentialExt.Length)
            pattern = pattern.Substring(0, pattern.Length - potentialExt.Length)
            Exit For
        Next
        '
        ' 2. Parse the pattern-string into its constitutent parts
        Dim patternSplit0 = pattern.Split({"%"c})
        Dim patternSplit As New List(Of String)
        If patternSplit0(0).Length > 0 Then patternSplit.Add(patternSplit0(0))
        For i = 1 To patternSplit0.Length - 1
            Dim s = "%" & patternSplit0(i)
            If Not s.StartsWith("%{") Then Console.WriteLine("ERROR: wrong pattern") : Return Nothing
            Dim ib = s.IndexOf("}")
            patternSplit.Add(s.Substring(0, ib + 1))
            If ib <> s.Length - 1 Then patternSplit.Add(s.Substring(ib + 1))
        Next

        For Each rsplit In patternSplit
            Dim part As New PatternPart
            part.pattern = rsplit

            If Not rsplit.StartsWith("%") Then
                part.generator = Function() rsplit
                part.matcher = Function(rr)
                                   If rr.Length < rsplit.Length Then Return -1
                                   If rr.Substring(0, rsplit.Length) = rsplit Then Return rsplit.Length
                                   Return -1
                               End Function
                Dim prevPart = r.PatternParts.LastOrDefault
                If prevPart IsNot Nothing AndAlso prevPart.matcher Is Nothing Then
                    prevPart.matcher = Function(rr)
                                           Dim i = rr.IndexOf(rsplit)
                                           If i = -1 Then Return rr.Length
                                           Return i
                                       End Function
                End If
                r.PatternParts.AddLast(part)
                Continue For
            End If

            If rsplit.StartsWith("%{fn}") Then
                part.generator = Function(fn2, dt2, pl2) fn2
                part.matcher = Nothing ' must be filled in by the next part
                r.PatternParts.AddLast(part)
                Continue For
            End If

            If rsplit.StartsWith("%{place}") Then
                part.generator = Function(fn2, dt2, pl2) pl2
                part.matcher = Nothing ' must be filled in by the next part
                r.PatternParts.AddLast(part)
                Continue For
            End If

            Dim escapes = {"%{datetime}", "yyyy.MM.dd - HH.mm.ss", "####.##.## - ##.##.##",
                                   "%{date}", "yyyy.MM.dd", "####.##.##",
                                   "%{time}", "HH.mm.ss", "##.##.##",
                                   "%{year}", "yyyy", "####",
                                   "%{month}", "MM", "##",
                                   "%{day}", "dd", "##",
                                   "%{hour}", "HH", "##",
                                   "%{minute}", "mm", "##",
                                   "%{second}", "ss", "##"}
            Dim escape = "", fmt = "", islike = ""
            For i = 0 To escapes.Length - 1 Step 3
                If Not rsplit.StartsWith(escapes(i)) Then Continue For
                escape = escapes(i)
                fmt = escapes(i + 1)
                islike = escapes(i + 2)
                Exit For
            Next
            If escape = "" Then Console.WriteLine("Unrecognized {0}", rsplit) : Return Nothing
            part.generator = Function(fn2, dt2, pl2) dt2.ToString(fmt)
            part.matcher = Function(rr)
                               If rr.Length < islike.Length Then Return -1
                               If rr.Substring(0, islike.Length) Like islike Then Return islike.Length
                               Return -1
                           End Function
            r.PatternParts.AddLast(part)
        Next

        ' The last part, if it was %{fn} or %{place} will match
        ' up to the remainder of the original filename
        Dim lastPart = r.PatternParts.Last.Value
        If lastPart.matcher Is Nothing Then lastPart.matcher = Function(rr) rr.Length



        Return r
    End Function

    Class Commandline
        Public Pattern As String
        Public Offset As TimeSpan?
        Public Fns As New List(Of String)
        '
        Public PatternParts As New LinkedList(Of PatternPart) ' derived from Pattern
        Public PatternExt As String ' derived from Pattern
    End Class

    Class PatternPart
        Public generator As PartGenerator
        Public matcher As MatchFunction
        Public pattern As String
    End Class

    Delegate Function PartGenerator(fn As String, dt As DateTime, place As String) As String
    Delegate Function MatchFunction(remainder As String) As Integer ' -1 for no-match, otherwise is the number of characters gobbled up
    Delegate Function UpdateTimeFunc(stream As IO.Stream, off As TimeSpan) As Boolean

    ReadOnly EmptyResult As New Tuple(Of DateTimeOffset2?, UpdateTimeFunc, GpsCoordinates)(Nothing, Function() False, Nothing)

    Class FileToDo
        Public fn As String
        Public localTime As DateTime
        Public setter As UpdateTimeFunc
        Public place As String
    End Class


    Dim http As HttpClient

    Function OpenStreetMapSearch(latitude As Double, longitude As Double) As String
        If http Is Nothing Then http = New HttpClient : http.DefaultRequestHeaders.Add("User-Agent", "TestReverseGeocoding")

        ' 1. Make the request
        Dim url = $"http://nominatim.openstreetmap.org/reverse?accept-language=en&format=xml&lat={latitude:0.000000}&lon={longitude:0.000000}&zoom=18"
        Dim raw = http.GetStringAsync(url).GetAwaiter().GetResult()
        Dim xml = XDocument.Parse(raw)

        ' 2. Parse the response
        Dim result = xml...<result>.@ref
        Dim road = xml...<road>.Value
        Dim neighbourhood = xml...<neighbourhood>.Value
        Dim suburb = xml...<suburb>.Value
        Dim city = xml...<city>.Value
        Dim county = xml...<county>.Value
        Dim state = xml...<state>.Value
        Dim country = xml...<country>.Value

        ' 3. Assemble these into a name
        Dim parts As New List(Of String)
        If result IsNot Nothing Then parts.Add(result) Else If road IsNot Nothing Then parts.Add(road)
        If suburb IsNot Nothing Then parts.Add(suburb) Else If neighbourhood IsNot Nothing Then parts.Add(neighbourhood)
        If city IsNot Nothing Then parts.Add(city) Else If county IsNot Nothing Then parts.Add(county)
        If country = "United States of America" OrElse country = "United Kingdom" Then parts.Add(state) Else parts.Add(country)
        Dim pi = 1
        While pi < parts.Count - 1
            If parts.Take(pi).Any(Function(s) s.Contains(parts(pi))) Then parts.RemoveAt(pi) Else pi += 1
        End While
        Return String.Join(", ", parts)
    End Function





    Function FilestampTime(fn As String) As Tuple(Of DateTimeOffset2, UpdateTimeFunc)
        Dim creationTime = IO.File.GetCreationTime(fn)
        Dim writeTime = IO.File.GetLastWriteTime(fn)
        Dim winnerTime = creationTime
        If writeTime < winnerTime Then winnerTime = writeTime
        Dim localTime = DateTimeOffset2.Utc(winnerTime.ToUniversalTime()) ' Although they're stored in UTC on disk, the APIs give us local-time
        '
        ' BUG COMPATIBILITY: Filestamp times are never as good as metadata times.
        ' Windows Phone doesn't store metadata, but it does use filenames of the form "WP_20131225".
        ' If we find that, we'll use it.
        Dim year = 0, month = 0, day = 0
        Dim hasWpName = False, usedFilenameTime = False
        If fn Like "*WP_20######*" Then
            Dim i = fn.IndexOf("WP_20") + 3
            If fn.Length >= i + 8 Then
                hasWpName = True
                Dim s = fn.Substring(i, 8)
                year = CInt(s.Substring(0, 4))
                month = CInt(s.Substring(4, 2))
                day = CInt(s.Substring(6, 2))

                If winnerTime.Year = year AndAlso winnerTime.Month = month AndAlso winnerTime.Day = day Then
                    ' good, the filestamp agrees with the filename
                Else
                    localTime = DateTimeOffset2.Unspecified(New DateTime(year, month, day, 12, 0, 0, System.DateTimeKind.Unspecified))
                    usedFilenameTime = True
                End If
            End If
        End If

        '
        Dim lambda As UpdateTimeFunc =
            Function(file2, off)
                If hasWpName Then
                    Dim nt = winnerTime + off
                    If usedFilenameTime OrElse nt.Year <> year OrElse nt.Month <> month OrElse nt.Day <> day Then
                        Console.WriteLine("Unable to modify time of file, since time was derived from filename") : Return False
                    End If
                End If

                IO.File.SetCreationTime(fn, creationTime + off)
                IO.File.SetLastWriteTime(fn, writeTime + off)
                Return True
            End Function

        Return Tuple.Create(localTime, lambda)
    End Function

    Class GpsCoordinates
        Public Latitude As Double
        Public Longitude As Double
        Public Sub New()
        End Sub
        Public Sub New(latitude As Double, longitude As Double)
            Me.Latitude = latitude
            Me.Longitude = longitude
        End Sub
        Public Overrides Function ToString() As String
            Return $"lat={Latitude:0.00} long={Longitude:0.00}"
        End Function
    End Class

    Function MetadataTimeAndGps(fn As String) As Tuple(Of DateTimeOffset2?, UpdateTimeFunc, GpsCoordinates)
        Using file As New IO.FileStream(fn, IO.FileMode.Open, IO.FileAccess.Read)
            file.Seek(0, IO.SeekOrigin.End) : Dim fend = file.Position
            If fend < 8 Then Return EmptyResult
            file.Seek(0, IO.SeekOrigin.Begin)
            Dim h1 = file.Read2byte(), h2 = file.Read2byte(), h3 = file.Read4byte()
            If h1 = &HFFD8 Then Return ExifTime(file, 0, fend) ' jpeg header
            If h3 = &H66747970 Then Return Mp4Time(file, 0, fend) ' "ftyp" prefix of mp4, mov
            Return Nothing
        End Using
    End Function

    Function ExifTime(file As IO.Stream, start As Long, fend As Long) As Tuple(Of DateTimeOffset2?, UpdateTimeFunc, GpsCoordinates)
        Dim timeLastModified, timeOriginal, timeDigitized As DateTime?
        Dim posLastModified = 0L, posOriginal = 0L, posDigitized = 0L
        Dim gpsNS = "", gpsEW = "", gpsLatVal As Double? = Nothing, gpsLongVal As Double? = Nothing

        Dim pos = start + 2
        While True ' Iterate through the EXIF markers
            If pos + 4 > fend Then Exit While
            file.Seek(pos, IO.SeekOrigin.Begin)
            Dim marker = file.Read2byte()
            Dim msize = file.Read2byte()
            'Console.WriteLine("EXIF MARKER {0:X}", marker)
            If pos + msize > fend Then Exit While
            Dim mbuf_pos = pos
            pos += 2 + msize
            If marker = &HFFDA Then Exit While ' image data follows this marker; we can stop our iteration
            If marker <> &HFFE1 Then Continue While ' we're only interested in exif markers
            If msize < 14 Then Continue While
            Dim exif1 = file.Read4byte() : If exif1 <> &H45786966 Then Continue While ' exif marker should start with this header "Exif"
            Dim exif2 = file.Read2byte() : If exif2 <> 0 Then Continue While ' and with this header
            Dim exif3 = file.Read4byte()
            Dim ExifDataIsLittleEndian = False
            If exif3 = &H49492A00 Then : ExifDataIsLittleEndian = True
            ElseIf exif3 = &H4D4D002A Then : ExifDataIsLittleEndian = False
            Else : Continue While : End If ' unrecognized byte-order
            Dim ipos = file.Read4byte(ExifDataIsLittleEndian)
            If ipos + 12 >= msize Then Continue While ' error  in tiff header
            '
            ' Format of EXIF is a chain of IFDs. Each consists of a number of tagged entries.
            ' One of the tagged entries may be "SubIFDpos = &H..." which gives the address of the
            ' next IFD in the chain; if this entry is absent or 0, then we're on the last IFD.
            ' Another tagged entry may be "GPSInfo = &H..." which gives the address of the GPS IFD
            '
            Dim subifdpos As UInteger = 0
            Dim gpsifdpos As UInteger = 0
            While True ' iterate through the IFDs
                'Console.WriteLine("  IFD @{0:X}\n", ipos)
                Dim ibuf_pos = mbuf_pos + 10 + ipos
                file.Seek(ibuf_pos, IO.SeekOrigin.Begin)
                Dim nentries = file.Read2byte(ExifDataIsLittleEndian)
                If 10 + ipos + 2 + nentries * 12 + 4 >= msize Then Exit While ' error in ifd header
                file.Seek(ibuf_pos + 2 + nentries * 12, IO.SeekOrigin.Begin)
                ipos = file.Read4byte(ExifDataIsLittleEndian)
                For i = 0 To nentries - 1
                    Dim ebuf_pos = ibuf_pos + 2 + i * 12
                    file.Seek(ebuf_pos, IO.SeekOrigin.Begin)
                    Dim tag = file.Read2byte(ExifDataIsLittleEndian)
                    Dim format = file.Read2byte(ExifDataIsLittleEndian)
                    Dim ncomps = file.Read4byte(ExifDataIsLittleEndian)
                    Dim data = file.Read4byte(ExifDataIsLittleEndian)
                    'Console.WriteLine("    TAG {0:X} format={1:X} ncomps={2:X} data={3:X}", tag, format, ncomps, data)
                    If tag = &H8769 AndAlso format = 4 Then
                        subifdpos = data
                    ElseIf tag = &H8825 AndAlso format = 4 Then
                        gpsifdpos = data
                    ElseIf (tag = 1 OrElse tag = 3) AndAlso format = 2 AndAlso ncomps = 2 Then
                        Dim s = ChrW(CInt(data >> 24))
                        If tag = 1 Then gpsNS = s Else gpsEW = s
                    ElseIf (tag = 2 OrElse tag = 4) AndAlso format = 5 AndAlso ncomps = 3 AndAlso 10 + data + ncomps < msize Then
                        Dim ddpos = mbuf_pos + 10 + data
                        file.Seek(ddpos, IO.SeekOrigin.Begin)
                        Dim degTop = file.Read4byte(ExifDataIsLittleEndian)
                        Dim degBot = file.Read4byte(ExifDataIsLittleEndian)
                        Dim minTop = file.Read4byte(ExifDataIsLittleEndian)
                        Dim minBot = file.Read4byte(ExifDataIsLittleEndian)
                        Dim secTop = file.Read4byte(ExifDataIsLittleEndian)
                        Dim secBot = file.Read4byte(ExifDataIsLittleEndian)
                        Dim deg = degTop / degBot + minTop / minBot / 60.0 + secTop / secBot / 3600.0
                        If tag = 2 Then gpsLatVal = deg
                        If tag = 4 Then gpsLongVal = deg
                    ElseIf (tag = &H132 OrElse tag = &H9003 OrElse tag = &H9004) AndAlso format = 2 AndAlso ncomps = 20 AndAlso 10 + data + ncomps < msize Then
                        Dim ddpos = mbuf_pos + 10 + data
                        file.Seek(ddpos, IO.SeekOrigin.Begin)
                        Dim buf(18) As Byte : file.Read(buf, 0, 19)
                        Dim s = Text.Encoding.ASCII.GetString(buf)
                        Dim dd As DateTime
                        If DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, dd) Then
                            If tag = &H132 Then timeLastModified = dd : posLastModified = ddpos
                            If tag = &H9003 Then timeOriginal = dd : posOriginal = ddpos
                            If tag = &H9004 Then timeDigitized = dd : posDigitized = ddpos
                            'Console.WriteLine("      {0}", dd)
                        End If
                    End If
                Next
                If ipos = 0 Then
                    ipos = subifdpos : subifdpos = 0
                    If ipos = 0 Then ipos = gpsifdpos : gpsifdpos = 0
                    If ipos = 0 Then Exit While ' indicates the last IFD in this marker
                End If
            End While
        End While

        Dim winnerTime = timeLastModified
        If Not winnerTime.HasValue OrElse (timeDigitized.HasValue AndAlso timeDigitized.Value < winnerTime.Value) Then winnerTime = timeDigitized
        If Not winnerTime.HasValue OrElse (timeOriginal.HasValue AndAlso timeOriginal.Value < winnerTime.Value) Then winnerTime = timeOriginal
        '
        Dim winnerTimeOffset = If(winnerTime.HasValue, DateTimeOffset2.Unspecified(winnerTime.Value), CType(Nothing, DateTimeOffset2?))

        Dim lambda As UpdateTimeFunc =
            Function(file2, off)
                If timeLastModified.HasValue AndAlso posLastModified <> 0 Then
                    Dim buf = Text.Encoding.ASCII.GetBytes((timeLastModified.Value + off).ToString("yyyy:MM:dd HH:mm:ss"))
                    file2.Seek(posLastModified, IO.SeekOrigin.Begin) : file2.Write(buf, 0, buf.Length)
                End If
                If timeOriginal.HasValue AndAlso posOriginal <> 0 Then
                    Dim buf = Text.Encoding.ASCII.GetBytes((timeOriginal.Value + off).ToString("yyyy:MM:dd HH:mm:ss"))
                    file2.Seek(posOriginal, IO.SeekOrigin.Begin) : file2.Write(buf, 0, buf.Length)
                End If
                If timeDigitized.HasValue AndAlso posDigitized <> 0 Then
                    Dim buf = Text.Encoding.ASCII.GetBytes((timeDigitized.Value + off).ToString("yyyy:MM:dd HH:mm:ss"))
                    file2.Seek(posDigitized, IO.SeekOrigin.Begin) : file2.Write(buf, 0, buf.Length)
                End If
                Return True
            End Function

        Dim gps As GpsCoordinates = Nothing
        If (gpsNS = "N" OrElse gpsNS = "S") AndAlso gpsLatVal.HasValue AndAlso (gpsEW = "E" OrElse gpsEW = "W") AndAlso gpsLongVal.HasValue Then
            gps = New GpsCoordinates
            gps.Latitude = If(gpsNS = "N", gpsLatVal.Value, -gpsLatVal.Value)
            gps.Longitude = If(gpsEW = "E", gpsLongVal.Value, -gpsLongVal.Value)
        End If

        Return Tuple.Create(winnerTimeOffset, lambda, gps)
    End Function

    Function Mp4Time(file As IO.Stream, start As Long, fend As Long) As Tuple(Of DateTimeOffset2?, UpdateTimeFunc, GpsCoordinates)
        ' The file is made up of a sequence of boxes, with a standard way to find size and FourCC "kind" of each.
        ' Some box kinds contain a kind-specific blob of binary data. Other box kinds contain a sequence
        ' of sub-boxes. You need to look up the specs for each kind to know whether it has a blob or sub-boxes.
        ' We look for a top-level box of kind "moov", which contains sub-boxes, and then we look for its sub-box
        ' of kind "mvhd", which contains a binary blob. This is where Creation/ModificationTime are stored.
        Dim pos = start, payloadStart = 0L, payloadEnd = 0L, boxKind = ""
        '
        While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "ftyp" : pos = payloadEnd
        End While
        If boxKind <> "ftyp" Then Return EmptyResult
        Dim majorBrandBuf(3) As Byte
        file.Seek(payloadStart, IO.SeekOrigin.Begin) : file.Read(majorBrandBuf, 0, 4)
        Dim majorBrand = Text.Encoding.ASCII.GetString(majorBrandBuf)
        '
        pos = start
        While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "moov" : pos = payloadEnd : End While
        If boxKind <> "moov" Then Return EmptyResult
        Dim moovStart = payloadStart, moovEnd = payloadEnd
        '
        pos = moovStart : fend = moovEnd
        While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "mvhd" : pos = payloadEnd : End While
        If boxKind <> "mvhd" Then Return EmptyResult
        Dim mvhdStart = payloadStart, mvhdEnd = payloadEnd
        '
        pos = moovStart : fend = moovEnd
        Dim cdayStart = 0L, cdayEnd = 0L
        Dim cnthStart = 0L, cnthEnd = 0L
        While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "udta" : pos = payloadEnd : End While
        If boxKind = "udta" Then
            Dim udtaStart = payloadStart, udtaEnd = payloadEnd
            '
            pos = udtaStart : fend = udtaEnd
            While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "©day" : pos = payloadEnd : End While
            If boxKind = "©day" Then cdayStart = payloadStart : cdayEnd = payloadEnd
            '
            pos = udtaStart : fend = udtaEnd
            While Mp4ReadNextBoxInfo(file, pos, fend, boxKind, payloadStart, payloadEnd) AndAlso boxKind <> "CNTH" : pos = payloadEnd : End While
            If boxKind = "CNTH" Then cnthStart = payloadStart : cnthEnd = payloadEnd
        End If

        ' The "mvhd" binary blob consists of 1byte (version, either 0 or 1), 3bytes (flags),
        ' and then either 4bytes (creation), 4bytes (modification)
        ' or 8bytes (creation), 8bytes (modification)
        ' If version=0 then it's the former, otherwise it's the later.
        ' In both cases "creation" and "modification" are big-endian number of seconds since 1st Jan 1904 UTC
        If mvhdEnd - mvhdStart < 20 Then Return EmptyResult
        file.Seek(mvhdStart + 0, IO.SeekOrigin.Begin) : Dim version = file.ReadByte(), numBytes = If(version = 0, 4, 8)
        file.Seek(mvhdStart + 4, IO.SeekOrigin.Begin)
        Dim creationFix1970 = False, modificationFix1970 = False
        Dim creationTime = file.ReadDate(numBytes, creationFix1970)
        Dim modificationTime = file.ReadDate(numBytes, modificationFix1970)
        ' COMPATIBILITY-BUG: The spec says that these times are in UTC.
        ' However, my Sony Cybershot merely gives them in unspecified time (i.e. local time but without specifying the timezone)
        ' Indeed its UI doesn't even let you say what the current UTC time is.
        ' I also noticed that my Sony Cybershot gives MajorBrand="MSNV", which isn't used by my iPhone or Canon or WP8.
        ' I'm going to guess that all "MSNV" files come from Sony, and all of them have the bug.
        Dim makeMvhdTime = Function(dt As DateTime) As DateTimeOffset2
                               If majorBrand = "MSNV" Then Return DateTimeOffset2.Unspecified(dt)
                               Return DateTimeOffset2.Utc(dt)
                           End Function

        ' The "©day" binary blob consists of 2byte (string-length, big-endian), 2bytes (language-code), string
        Dim dayTime As DateTimeOffset2? = Nothing
        Dim cdayStringLen = 0, cdayString = ""
        If cdayStart <> 0 AndAlso cdayEnd - cdayStart > 4 Then
            file.Seek(cdayStart + 0, IO.SeekOrigin.Begin)
            cdayStringLen = file.Read2byte()
            If cdayStart + 4 + cdayStringLen <= cdayEnd Then
                file.Seek(cdayStart + 4, IO.SeekOrigin.Begin)
                Dim buf = New Byte(cdayStringLen - 1) {}
                file.Read(buf, 0, cdayStringLen)
                cdayString = System.Text.Encoding.ASCII.GetString(buf)
                Dim d As DateTimeOffset : If DateTimeOffset.TryParse(cdayString, d) Then dayTime = DateTimeOffset2.Local(d)
            End If
        End If

        ' The "CNTH" binary blob consists of 8bytes of unknown, followed by EXIF data
        Dim cnthTime As DateTimeOffset2? = Nothing, cnthLambda As UpdateTimeFunc = Nothing, cnthGps As GpsCoordinates = Nothing
        If cnthStart <> 0 AndAlso cnthEnd - cnthStart > 16 Then
            Dim exif_ft = ExifTime(file, cnthStart + 8, cnthEnd)
            cnthTime = exif_ft.Item1 : cnthLambda = exif_ft.Item2 : cnthGps = exif_ft.Item3
        End If

        Dim winnerTime As DateTimeOffset2? = Nothing
        If dayTime.HasValue Then
            Debug.Assert(dayTime.Value.dt.Kind = System.DateTimeKind.Local)
            winnerTime = dayTime
            ' prefer this best of all because it knows local time and timezone
        ElseIf cnthTime.HasValue Then
            Debug.Assert(cnthTime.Value.dt.Kind = System.DateTimeKind.Unspecified)
            winnerTime = cnthTime
            ' this is second-best because it knows local time, just not timezone
        Else
            ' Otherwise, we'll make do with a UTC time, where we don't know local-time when the pic was taken, nor timezone
            If creationTime.HasValue AndAlso modificationTime.HasValue Then
                winnerTime = makeMvhdTime(If(creationTime < modificationTime, creationTime.Value, modificationTime.Value))
            ElseIf creationTime.HasValue Then
                winnerTime = makeMvhdTime(creationTime.Value)
            ElseIf modificationTime.HasValue Then
                winnerTime = makeMvhdTime(modificationTime.Value)
            End If
        End If

        Dim lambda As UpdateTimeFunc =
            Function(file2, offset)
                If creationTime.HasValue Then
                    Dim dd = creationTime.Value + offset
                    file2.Seek(mvhdStart + 4, IO.SeekOrigin.Begin)
                    file2.WriteDate(numBytes, dd, creationFix1970)
                End If
                If modificationTime.HasValue Then
                    Dim dd = modificationTime.Value + offset
                    file2.Seek(mvhdStart + 4 + numBytes, IO.SeekOrigin.Begin)
                    file2.WriteDate(numBytes, dd, modificationFix1970)
                End If
                If Not String.IsNullOrWhiteSpace(cdayString) Then
                    Dim dd As DateTimeOffset
                    If DateTimeOffset.TryParse(cdayString, dd) Then
                        dd = dd + offset
                        Dim str2 = dd.ToString("yyyy-MM-ddTHH:mm:sszz00")
                        Dim buf2 = Text.Encoding.ASCII.GetBytes(str2)
                        If buf2.Length = cdayStringLen Then
                            file2.Seek(cdayStart + 4, IO.SeekOrigin.Begin)
                            file2.Write(buf2, 0, buf2.Length)
                        End If
                    End If
                End If
                If cnthLambda IsNot Nothing Then cnthLambda(file2, offset)
                Return True
            End Function

        Return Tuple.Create(winnerTime, lambda, cnthGps)
    End Function


    Function Mp4ReadNextBoxInfo(f As IO.Stream, pos As Long, fend As Long, ByRef boxKind As String, ByRef payloadStart As Long, ByRef payloadEnd As Long) As Boolean
        boxKind = "" : payloadStart = 0 : payloadEnd = 0
        If pos + 8 > fend Then Return False
        Dim b(3) As Byte
        f.Seek(pos, IO.SeekOrigin.Begin)
        f.Read(b, 0, 4) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
        Dim size = BitConverter.ToUInt32(b, 0)
        f.Read(b, 0, 4)
        Dim kind = ChrW(b(0)) & ChrW(b(1)) & ChrW(b(2)) & ChrW(b(3))
        If size <> 1 Then
            If pos + size > fend Then Return False
            boxKind = kind : payloadStart = pos + 8 : payloadEnd = payloadStart + size - 8 : Return True
        End If
        If size = 1 AndAlso pos + 16 <= fend Then
            ReDim b(7)
            f.Read(b, 0, 8) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
            Dim size2 = CLng(BitConverter.ToUInt64(b, 0))
            If pos + size2 > fend Then Return False
            boxKind = kind : payloadStart = pos + 16 : payloadEnd = payloadStart + size2 - 16 : Return True
        End If
        Return False
    End Function

    <Runtime.CompilerServices.Extension>
    Sub Add(Of T, U, V)(this As LinkedList(Of Tuple(Of T, U, V)), arg1 As T, arg2 As U, arg3 As V)
        this.AddLast(Tuple.Create(Of T, U, V)(arg1, arg2, arg3))
    End Sub

    ReadOnly TZERO_1904_UTC As DateTime = New DateTime(1904, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
    ReadOnly TZERO_1970_UTC As DateTime = New DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)

    <Runtime.CompilerServices.Extension>
    Function Read2byte(f As IO.Stream, Optional fileIsLittleEndian As Boolean = False) As UShort
        Dim b(1) As Byte
        f.Read(b, 0, 2) : If BitConverter.IsLittleEndian <> fileIsLittleEndian Then Array.Reverse(b)
        Return BitConverter.ToUInt16(b, 0)
    End Function

    <Runtime.CompilerServices.Extension>
    Function Read4byte(f As IO.Stream, Optional fileIsLittleEndian As Boolean = False) As UInteger
        Dim b(3) As Byte
        f.Read(b, 0, 4) : If BitConverter.IsLittleEndian <> fileIsLittleEndian Then Array.Reverse(b)
        Return BitConverter.ToUInt32(b, 0)
    End Function

    <Runtime.CompilerServices.Extension>
    Function ReadDate(f As IO.Stream, numBytes As Integer, ByRef fixed1970 As Boolean) As DateTime?
        ' COMPATIBILITY-BUG: The spec says that these are expressed in seconds since 1904.
        ' But my brother's Android phone picks them in seconds since 1970.
        ' I'm going to guess that all dates before 1970 should be 66 years in the future
        ' Note: I'm applying this correction *before* converting to date. That's because,
        ' what with leap-years and stuff, it doesn't feel safe the other way around.
        If numBytes = 4 Then
            Dim b(3) As Byte
            f.Read(b, 0, 4) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
            Dim secs = BitConverter.ToUInt32(b, 0)
            If secs = 0 Then Return Nothing
            fixed1970 = (secs < (TZERO_1970_UTC - TZERO_1904_UTC).TotalSeconds)
            Return If(fixed1970, TZERO_1970_UTC.AddSeconds(secs), TZERO_1904_UTC.AddSeconds(secs))
        ElseIf numBytes = 8 Then
            Dim b(7) As Byte
            f.Read(b, 0, 8) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
            Dim secs = BitConverter.ToUInt64(b, 0)
            If secs = 0 Then Return Nothing
            fixed1970 = (secs < (TZERO_1970_UTC - TZERO_1904_UTC).TotalSeconds)
            Return If(fixed1970, TZERO_1970_UTC.AddSeconds(secs), TZERO_1904_UTC.AddSeconds(secs))
        Else
            Throw New ArgumentException("numBytes")
        End If
    End Function

    <Runtime.CompilerServices.Extension>
    Sub WriteDate(f As IO.Stream, numBytes As Integer, d As DateTime, fix1970 As Boolean)
        If d.Kind <> System.DateTimeKind.Utc Then Throw New ArgumentException("Can only write UTC dates")
        If numBytes = 4 Then
            Dim secs = CUInt(If(fix1970, d - TZERO_1970_UTC, d - TZERO_1904_UTC).TotalSeconds)
            Dim b = BitConverter.GetBytes(secs) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
            f.Write(b, 0, 4)
        ElseIf numBytes = 8 Then
            Dim secs = CULng(If(fix1970, d - TZERO_1970_UTC, d - TZERO_1904_UTC).TotalSeconds)
            Dim b = BitConverter.GetBytes(secs) : If BitConverter.IsLittleEndian Then Array.Reverse(b)
            f.Write(b, 0, 8)
        Else
            Throw New ArgumentException("numBytes")
        End If
    End Sub

    Structure DateTimeOffset2
        Public dt As DateTime
        Public offset As TimeSpan
        ' Three modes:
        ' (1) Time known to be in UTC: DateTime.Kind=UTC, offset=0
        ' (2) Time known to be in some specific timezone: DateTime.Kind=Local, offset gives that timezone
        ' (3) Time where nothing about timezone is known: DateTime.Kind=Unspecified, offset=0

        Shared Function Utc(d As DateTime) As DateTimeOffset2
            Dim d2 As New DateTime(d.Ticks, System.DateTimeKind.Utc)
            Return New DateTimeOffset2 With {.dt = d2, .offset = Nothing}
        End Function
        Shared Function Unspecified(d As DateTime) As DateTimeOffset2
            Dim d2 As New DateTime(d.Ticks, System.DateTimeKind.Unspecified)
            Return New DateTimeOffset2 With {.dt = d2, .offset = Nothing}
        End Function
        Shared Function Local(d As DateTimeOffset) As DateTimeOffset2
            Dim d2 As New DateTime(d.Ticks, System.DateTimeKind.Local)
            Return New DateTimeOffset2 With {.dt = d2, .offset = d.Offset}
        End Function

        Public Overrides Function ToString() As String
            If dt.Kind = System.DateTimeKind.Utc Then
                Return dt.ToString("yyyy:MM:ddTHH:mm:ssZ")
            ElseIf dt.Kind = System.DateTimeKind.Unspecified Then
                Return dt.ToString("yyyy:MM:dd HH:mm:ss")
            ElseIf dt.Kind = System.DateTimeKind.Local Then
                Return dt.ToString("yyyy:MM:dd HH:mm:ss") & offset.Hours.ToString("+00;-00") & "00"
            Else
                Throw New Exception("Invalid DateTimeKind")
            End If
        End Function
    End Structure



End Module

