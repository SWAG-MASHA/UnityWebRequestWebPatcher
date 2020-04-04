# UnityWebRequestWebPatcher
## (Tested for 2018.4.1-2018.4.19, C# 4.x Equivalent, .NET Standard 2.0)

Unity WebGL builds have a bug with POST request sending. 
It mangles request body in certain browsers on certain systems. For more info - see this Unity thread https://forum.unity.com/threads/mangled-post-requests-with-unitywebrequest-on-2018-4-x.858925/

This is a **reference implementation** of a `wasm.framework.unityweb` file patching. Place it in any `Editor` folder in your project.
________________________________________________________

It contains three methods that can be used to either manually patch the framework file after build, or can be included in your custom build pipeline as the last step.

`PatchDevelopmentFrameworkCode` - patches the unminified and uncompressed framework code. This is the "raw" code that `UnityJS.js` and `Unity.asm.js` files have.

`PatchUncompressedReleaseFrameworkCode` - patches the minified, but uncompressed framework code. This is what you get when you choose `Compression Format - Disabled` in the project settings of your WebGL target.

`PatchGzipCompressedReleaseFrameworkCode` - patches the minified and GZip compressed framework code. This is what you get when you choose `Compression Format - GZip` in the project settings of your WebGL target.

It uses `System.IO.Compression.GZipStream` API that doesn't compress as good as does the embedded 7za tool, but it's a crossplatform solution that doesn't require different code for process creation on Mac, Windows and Linux
_________________________________________________________

Patching works by replacing the code that tries to send raw bytes over the `http.send`. It manually converts those bytes to a UTF-8 string using the [`TextDecoder`](https://developer.mozilla.org/en-US/docs/Web/API/TextDecoder) functionality ([Can I use it?](https://caniuse.com/#feat=mdn-api_textdecoder)).

### Please note that this is only a reference implementation that may need some adjustments in your project depending on your scripting api level and Unity version (different versions may contain different framework code)
