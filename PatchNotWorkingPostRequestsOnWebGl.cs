using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PatchNotWorkingPostRequestsOnWebGl
{
    const string releaseStringToReplace = "function _JS_WebRequest_Send(request,ptr,length){var http=wr.requestInstances[request];try{if(length>0)http.send(HEAPU8.subarray(ptr,ptr+length));else http.send()}catch(e){console.error(e.name+\": \"+e.message)}}";
    const string debugStringToReplace = @"function _JS_WebRequest_Send(request, ptr, length) {
 var http = wr.requestInstances[request];
 try {
  if (length > 0) http.send(HEAPU8.subarray(ptr, ptr + length)); else http.send();
 } catch (e) {
  console.error(e.name + "": "" + e.message);
 }
}";
    
    const string releaseStringPatched = "function _JS_WebRequest_Send(request,ptr,length){var td=new TextDecoder('utf-8');var sp=td.decode(HEAPU8.subarray(ptr,ptr+length));var http=wr.requestInstances[request];try{if(length>0)http.send(sp);else http.send()}catch(e){console.error(e.name+\": \"+e.message)}}";
    private const string debugStringPatched = @"function _JS_WebRequest_Send(request, ptr, length) {
 var td = new TextDecoder('utf-8');
 var sp = td.decode(HEAPU8.subarray(ptr, ptr + length));
 var http = wr.requestInstances[request];
 try {
  if (length > 0) http.send(sp); else http.send();
 } catch (e) {
  console.error(e.name + "": "" + e.message);
 }
}";

    private static string GetFrameworkToPatchPath(string buildPath)
    {
        var buildDirInfo = new DirectoryInfo(buildPath);
        var buildDirName = buildDirInfo.Name;
        var frameworkFileName = $"{buildDirName}.wasm.framework.unityweb";
        var frameworkToPatchPath = Path.Combine(buildPath, "Build", frameworkFileName);
        return frameworkToPatchPath;
    }

    public static void PatchFrameworkCode(string frameworkToPatchPath, string stringToReplace, string replaceWith)
    {
        var frameworkString = File.ReadAllText(frameworkToPatchPath);
        frameworkString = frameworkString.Replace(stringToReplace, replaceWith);
        File.WriteAllText(frameworkToPatchPath, frameworkString);
    }

    public static void PatchDevelopmentFrameworkCode(string buildPath)
    {
        var frameworkToPatchPath = GetFrameworkToPatchPath(buildPath);
        PatchFrameworkCode(frameworkToPatchPath, debugStringToReplace, debugStringPatched);
    }
    
    public static void PatchUncompressedReleaseFrameworkCode(string buildPath)
    {
        var frameworkToPatchPath = GetFrameworkToPatchPath(buildPath);
        PatchFrameworkCode(frameworkToPatchPath, releaseStringToReplace, releaseStringPatched);
    }

    public static void PatchGzipCompressedReleaseFrameworkCode(string buildPath)
    {
        var frameworkToPatchPath = GetFrameworkToPatchPath(buildPath);
        var frameworkFileInfo = new FileInfo(frameworkToPatchPath);
        var extractToFolder = Directory.CreateDirectory(Path.Combine(frameworkFileInfo.Directory.FullName, "temp"));
        var extractedFramework = Path.Combine(extractToFolder.FullName, frameworkFileInfo.Name);
        Extract7zContents(frameworkToPatchPath, extractToFolder.FullName);
        PatchFrameworkCode(extractedFramework, releaseStringToReplace, releaseStringPatched);
        File.Delete(frameworkToPatchPath);
        CompressGzipToFile(extractedFramework, frameworkToPatchPath);
        Directory.Delete(extractToFolder.FullName, true);
        AddArchiveFileNameAndComment(frameworkFileInfo);
    }

    public static void AddArchiveFileNameAndComment(FileInfo frameworkFileInfo)
    {
        var archiveBytes = File.ReadAllBytes(frameworkFileInfo.FullName).ToList();
        const int filenameOffset = 10;
        
        // Adding a "FNAME" flag to the FLAGS byte
        archiveBytes[3] |= 0x08;
        var filenameBytes = Encoding.ASCII.GetBytes(frameworkFileInfo.Name + "\0");
        // Adding a zero terminated filename
        archiveBytes.InsertRange(filenameOffset, filenameBytes);
        
        // Adding a "FCOMMENT" flag to the FLAGS byte
        archiveBytes[3] |= 0x10;
        // Adding a zero terminated comment after the filename
        var comment = Encoding.ASCII.GetBytes("UnityWeb Compressed Content (gzip)\0");
        var commentOffset = filenameOffset + filenameBytes.Length;
        archiveBytes.InsertRange(commentOffset, comment);
        
        File.WriteAllBytes(frameworkFileInfo.FullName, archiveBytes.ToArray());
    }

    public static void Extract7zContents(string fileToExtractFrom, string folderToExtractTo)
    {
        var archiveInfo = new FileInfo(fileToExtractFrom);
        var fileToExtractToPath = Path.Combine(folderToExtractTo, archiveInfo.Name);
        
        using(var archiveStream = archiveInfo.OpenRead())
        using(var decompressStream = File.Create(fileToExtractToPath))
        using (var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress))
        {
            gzipStream.CopyTo(decompressStream);
        }
    }

    public static void CompressGzipToFile(string fileToArchive, string archive)
    {
        using (var fileToArchiveStream = File.OpenRead(fileToArchive))
        using (var archiveStream = File.Create(archive))
        using (var gzipStream = new GZipStream(archiveStream, CompressionMode.Compress))
        {
            fileToArchiveStream.CopyTo(gzipStream);
        }
    }

    [MenuItem("Tools/Patch WebGL Development", priority = 15000)]
    public static void PatchDevelopmentBuild()
    {
        GenericPatch(PatchDevelopmentFrameworkCode);
    }
    
    [MenuItem("Tools/Patch WebGL UNCOMPRESSED Release", priority = 15000)]
    public static void PatchUncompressedReleaseBuild()
    {
        GenericPatch(PatchUncompressedReleaseFrameworkCode);
    }
    
    [MenuItem("Tools/Patch WebGL GZIP Release", priority = 15000)]
    public static void PatchGzipCompressedReleaseBuild()
    {
        GenericPatch(PatchGzipCompressedReleaseFrameworkCode);
    } 

    public static void GenericPatch(Action<string> patchAction)
    {
        var projectRootFolder = new DirectoryInfo(Application.dataPath).Parent.FullName;
        var selectedBuildFolder = EditorUtility.OpenFolderPanel("Select build root folder (with index.html)", projectRootFolder, "Build");
        if (String.IsNullOrEmpty(selectedBuildFolder))
        {
            Debug.LogError("No folder selected");
            return;
        }
        
        patchAction(selectedBuildFolder);
        Debug.Log("Patched");
    }
}
