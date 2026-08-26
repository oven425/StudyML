// ConsoleApplication_Audio.cpp : 此檔案包含 'main' 函式。程式會於該處開始執行及結束執行。
//

#include <iostream>
#include <mmdeviceapi.h>
#include <Audioclient.h>
int main()
{
    //AUDCLNT_E_NOT_INITIALIZED
    const CLSID CLSID_MMDeviceEnumerator = __uuidof(MMDeviceEnumerator);
    const IID IID_IMMDeviceEnumerator = __uuidof(IMMDeviceEnumerator);
    HRESULT hr = S_OK;
    IMMDeviceEnumerator* pEnumerator = NULL;
    IMMDeviceCollection* pCollection = NULL;
    IMMDevice* pEndpoint = NULL;
    IPropertyStore* pProps = NULL;
    LPWSTR pwszID = NULL;
    CoInitializeEx(NULL, 2);
    hr = CoCreateInstance(
        CLSID_MMDeviceEnumerator, NULL,
        CLSCTX_ALL, IID_IMMDeviceEnumerator,
        (void**)&pEnumerator);

        hr = pEnumerator->EnumAudioEndpoints(
            eRender, DEVICE_STATE_ACTIVE,
            &pCollection);

        UINT  count;
    hr = pCollection->GetCount(&count);
    //https://learn.microsoft.com/zh-tw/windows/win32/coreaudio/capturing-a-stream
    IMMDevice* mmdevice = nullptr;
    pEnumerator->GetDefaultAudioEndpoint(EDataFlow::eRender, ERole::eConsole, &mmdevice);
    IAudioClient* audioclient;
    hr = mmdevice->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, (void**)&audioclient);
    WAVEFORMATEX* wavef = nullptr;
    hr = audioclient->GetMixFormat(&wavef);
    int REFTIMES_PER_SEC = 10000000;
    hr = audioclient->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, REFTIMES_PER_SEC, 0, wavef, NULL);
    IAudioCaptureClient* pCaptureClient = NULL;
    hr = audioclient->GetService(__uuidof(IAudioCaptureClient), (void**)&pCaptureClient);

    hr = audioclient->Start();
    BYTE* pData;
    UINT32 numFrames;
    DWORD flags;
    DWORD totalBytes = 0;
    while (1) {
        Sleep(100);
        hr = pCaptureClient->GetBuffer(&pData, &numFrames, &flags, NULL, NULL);
        if (SUCCEEDED(hr) && numFrames > 0) {

            pCaptureClient->ReleaseBuffer(numFrames);
        }
    }
}

// 執行程式: Ctrl + F5 或 [偵錯] > [啟動但不偵錯] 功能表
// 偵錯程式: F5 或 [偵錯] > [啟動偵錯] 功能表

// 開始使用的提示: 
//   1. 使用 [方案總管] 視窗，新增/管理檔案
//   2. 使用 [Team Explorer] 視窗，連線到原始檔控制
//   3. 使用 [輸出] 視窗，參閱組建輸出與其他訊息
//   4. 使用 [錯誤清單] 視窗，檢視錯誤
//   5. 前往 [專案] > [新增項目]，建立新的程式碼檔案，或是前往 [專案] > [新增現有項目]，將現有程式碼檔案新增至專案
//   6. 之後要再次開啟此專案時，請前往 [檔案] > [開啟] > [專案]，然後選取 .sln 檔案
