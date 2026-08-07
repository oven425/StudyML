using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace ConsoleApp_MAF
{
    public class Audio
    {
        static async public Task To(string inputname)
        {
            var inputname_full = System.IO.Path.GetFullPath(inputname);
            StorageFile inputFile = await StorageFile.GetFileFromPathAsync(inputname_full);

            var dir = System.IO.Path.GetDirectoryName(inputname_full);
            var filename = $"{System.IO.Path.GetFileNameWithoutExtension(inputname_full)}.wav";
            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(dir);
            StorageFile outputFile = await outputFolder.CreateFileAsync(
                filename,
                CreationCollisionOption.ReplaceExisting
            );

            AudioEncodingProperties audioProps = AudioEncodingProperties.CreatePcm(
                16000, // 採樣率 (Sample Rate)
                1,     // 聲道數 (1 = Mono)
                16     // 位元深度 (16-bit)
            );
            MediaEncodingProfile profile = new MediaEncodingProfile();
            profile.Container.Subtype = MediaEncodingSubtypes.Wave;
            profile.Audio = audioProps;

            MediaTranscoder transcoder = new MediaTranscoder();
            transcoder.HardwareAccelerationEnabled = true;

            PrepareTranscodeResult prepareResult = await transcoder.PrepareFileTranscodeAsync(
                    inputFile,
                    outputFile,
                    profile
                );

            if (prepareResult.CanTranscode)
            {
                await prepareResult.TranscodeAsync();
                //return true;
            }
            else
            {
                //std::wcerr << L"Transcode failed, reason: "
                //          << static_cast<int>(prepareResult.FailureReason()) << std::endl;
                //co_return false;
            }
        }
    }
}
