/* dupcap: captures one monitor as Windows composed it (DXGI Desktop Duplication), for comparing
 * how the original game and Moono reach the screen (tools/HdrCheck/README.md).
 *
 *   dupcap X Y sdr OUT.ppm            8-bit BGRA (IDXGIOutput5::DuplicateOutput1, else
 *                                     IDXGIOutput1::DuplicateOutput), written as a binary PPM
 *   dupcap X Y fp16 OUT.pfm [RX RY RW RH]
 *                                     FP16 scRGB (DuplicateOutput1 with R16G16B16A16_FLOAT: linear,
 *                                     1.0 = 80 nits), written as a PFM; prints the mean luminance
 *                                     in nits of the region (output-relative pixels; default: all)
 *
 * The monitor is the output whose desktop rectangle holds X,Y. No admin rights needed. Read-only:
 * it changes no display, HDR or driver setting. Built by build.cmd (MSVC).
 */
#define COBJMACROS
#define CINTERFACE
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_6.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

static int fail(const char *what, HRESULT hr)
{
    fprintf(stderr, "dupcap: %s failed (0x%08lx)\n", what, (unsigned long)hr);
    return 1;
}

static float half_to_float(unsigned short h)
{
    unsigned sign = (h >> 15) & 1, exponent = (h >> 10) & 0x1f, mantissa = h & 0x3ff;
    float v;
    if (exponent == 0) v = (float)mantissa / 1024.0f / 16384.0f;
    else if (exponent == 31) v = mantissa ? 0.0f : 65504.0f;
    else v = (1.0f + (float)mantissa / 1024.0f) * (float)ldexp(1.0, (int)exponent - 15);
    return sign ? -v : v;
}

static float srgb_to_linear(unsigned char v)
{
    double c = v / 255.0;
    return (float)(c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4));
}

int main(int argc, char **argv)
{
    if (argc < 5) {
        fprintf(stderr, "usage: dupcap X Y sdr|fp16 OUT [RX RY RW RH]\n");
        return 2;
    }
    int px = atoi(argv[1]), py = atoi(argv[2]);
    int fp16 = strcmp(argv[3], "fp16") == 0;
    const char *out = argv[4];
    /* DuplicateOutput1 needs per-monitor DPI awareness v2. */
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    IDXGIFactory1 *factory = NULL;
    HRESULT hr = CreateDXGIFactory1(&IID_IDXGIFactory1, (void **)&factory);
    if (FAILED(hr)) return fail("CreateDXGIFactory1", hr);
    IDXGIAdapter1 *adapter = NULL;
    IDXGIOutput *output = NULL;
    DXGI_OUTPUT_DESC od;
    for (UINT a = 0; !output && IDXGIFactory1_EnumAdapters1(factory, a, &adapter) == S_OK; a++) {
        IDXGIOutput *o = NULL;
        for (UINT i = 0; IDXGIAdapter1_EnumOutputs(adapter, i, &o) == S_OK; i++) {
            IDXGIOutput_GetDesc(o, &od);
            RECT r = od.DesktopCoordinates;
            if (px >= r.left && px < r.right && py >= r.top && py < r.bottom) { output = o; break; }
            IDXGIOutput_Release(o);
        }
        if (!output) { IDXGIAdapter1_Release(adapter); adapter = NULL; }
    }
    if (!output) { fprintf(stderr, "dupcap: no output holds %d,%d\n", px, py); return 1; }

    ID3D11Device *device = NULL;
    ID3D11DeviceContext *context = NULL;
    hr = D3D11CreateDevice((IDXGIAdapter *)adapter, D3D_DRIVER_TYPE_UNKNOWN, NULL, 0, NULL, 0, D3D11_SDK_VERSION, &device, NULL, &context);
    if (FAILED(hr)) return fail("D3D11CreateDevice", hr);

    IDXGIOutputDuplication *dup = NULL;
    IDXGIOutput5 *output5 = NULL;
    DXGI_FORMAT format = fp16 ? DXGI_FORMAT_R16G16B16A16_FLOAT : DXGI_FORMAT_B8G8R8A8_UNORM;
    if (SUCCEEDED(IDXGIOutput_QueryInterface(output, &IID_IDXGIOutput5, (void **)&output5))) {
        hr = IDXGIOutput5_DuplicateOutput1(output5, (IUnknown *)device, 0, 1, &format, &dup);
        IDXGIOutput5_Release(output5);
        if (FAILED(hr)) return fail("DuplicateOutput1", hr);
    } else if (fp16) {
        fprintf(stderr, "dupcap: no IDXGIOutput5 (FP16 capture needs Windows 10 1803 or later)\n");
        return 1;
    } else {
        IDXGIOutput1 *output1 = NULL;
        hr = IDXGIOutput_QueryInterface(output, &IID_IDXGIOutput1, (void **)&output1);
        if (FAILED(hr)) return fail("QueryInterface IDXGIOutput1", hr);
        hr = IDXGIOutput1_DuplicateOutput(output1, (IUnknown *)device, &dup);
        IDXGIOutput1_Release(output1);
        if (FAILED(hr)) return fail("DuplicateOutput", hr);
    }

    /* A frame that updated the image (LastPresentTime set): one that only moved the pointer holds
     * no image. Up to 5 s: a still desktop updates nothing. */
    DXGI_OUTDUPL_FRAME_INFO info;
    IDXGIResource *resource = NULL;
    for (int tries = 0; tries < 50; tries++) {
        hr = IDXGIOutputDuplication_AcquireNextFrame(dup, 100, &info, &resource);
        if (hr == DXGI_ERROR_WAIT_TIMEOUT) continue;
        if (FAILED(hr)) return fail("AcquireNextFrame", hr);
        if (info.LastPresentTime.QuadPart != 0) break;
        IDXGIResource_Release(resource);
        resource = NULL;
        IDXGIOutputDuplication_ReleaseFrame(dup);
    }
    if (!resource) { fprintf(stderr, "dupcap: no image update in 5 s\n"); return 1; }
    ID3D11Texture2D *frame = NULL;
    hr = IDXGIResource_QueryInterface(resource, &IID_ID3D11Texture2D, (void **)&frame);
    if (FAILED(hr)) return fail("QueryInterface ID3D11Texture2D", hr);
    D3D11_TEXTURE2D_DESC desc;
    ID3D11Texture2D_GetDesc(frame, &desc);
    desc.Usage = D3D11_USAGE_STAGING;
    desc.BindFlags = 0;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    desc.MiscFlags = 0;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    ID3D11Texture2D *staging = NULL;
    hr = ID3D11Device_CreateTexture2D(device, &desc, NULL, &staging);
    if (FAILED(hr)) return fail("CreateTexture2D (staging)", hr);
    ID3D11DeviceContext_CopyResource(context, (ID3D11Resource *)staging, (ID3D11Resource *)frame);
    D3D11_MAPPED_SUBRESOURCE map;
    hr = ID3D11DeviceContext_Map(context, (ID3D11Resource *)staging, 0, D3D11_MAP_READ, 0, &map);
    if (FAILED(hr)) return fail("Map", hr);

    int w = (int)desc.Width, h = (int)desc.Height;
    /* DWM hands over 8-bit BGRA, whatever was asked, for a monitor with HDR off. */
    int halfFloats = desc.Format == DXGI_FORMAT_R16G16B16A16_FLOAT;
    if (!halfFloats && desc.Format != DXGI_FORMAT_B8G8R8A8_UNORM) {
        fprintf(stderr, "dupcap: unexpected frame format %d\n", (int)desc.Format);
        return 1;
    }
    if (!fp16 && halfFloats) { fprintf(stderr, "dupcap: an FP16 frame for an 8-bit capture\n"); return 1; }
    if (fp16 && !halfFloats)
        fprintf(stderr, "dupcap: the monitor gave an 8-bit frame (HDR is off on it): values are sRGB decoded, 1.0 = SDR white, reported as 80 nits\n");
    FILE *f = fopen(out, "wb");
    if (!f) { fprintf(stderr, "dupcap: cannot write %s\n", out); return 1; }
    if (!fp16) {
        fprintf(f, "P6\n%d %d\n255\n", w, h);
        unsigned char *row = malloc((size_t)w * 3);
        for (int y = 0; y < h; y++) {
            const unsigned char *src = (const unsigned char *)map.pData + (size_t)y * map.RowPitch;
            for (int x = 0; x < w; x++) { row[x * 3] = src[x * 4 + 2]; row[x * 3 + 1] = src[x * 4 + 1]; row[x * 3 + 2] = src[x * 4]; }
            fwrite(row, 1, (size_t)w * 3, f);
        }
        free(row);
        printf("%dx%d sdr (format %d) -> %s\n", w, h, (int)desc.Format, out);
    } else {
        int rx = 0, ry = 0, rw = w, rh = h;
        if (argc >= 9) { rx = atoi(argv[5]); ry = atoi(argv[6]); rw = atoi(argv[7]); rh = atoi(argv[8]); }
        /* PFM rows run bottom-up. */
        fprintf(f, "PF\n%d %d\n-1.0\n", w, h);
        float *row = malloc((size_t)w * 3 * sizeof(float));
        double sum = 0, maxNits = 0;
        long count = 0;
        for (int y = h - 1; y >= 0; y--) {
            const unsigned char *line = (const unsigned char *)map.pData + (size_t)y * map.RowPitch;
            const unsigned short *src = (const unsigned short *)line;
            for (int x = 0; x < w; x++) {
                float r, g, b;
                if (halfFloats) { r = half_to_float(src[x * 4]); g = half_to_float(src[x * 4 + 1]); b = half_to_float(src[x * 4 + 2]); }
                else { r = srgb_to_linear(line[x * 4 + 2]); g = srgb_to_linear(line[x * 4 + 1]); b = srgb_to_linear(line[x * 4]); }
                row[x * 3] = r; row[x * 3 + 1] = g; row[x * 3 + 2] = b;
                if (x >= rx && x < rx + rw && y >= ry && y < ry + rh) {
                    /* scRGB is linear BT.709; 1.0 is 80 nits. */
                    double nits = 80.0 * (0.2126 * r + 0.7152 * g + 0.0722 * b);
                    sum += nits;
                    if (nits > maxNits) maxNits = nits;
                    count++;
                }
            }
            fwrite(row, sizeof(float), (size_t)w * 3, f);
        }
        free(row);
        printf("%dx%d fp16 (format %d) -> %s\nregion %d,%d %dx%d: mean %.1f nits, max %.1f nits\n",
            w, h, (int)desc.Format, out, rx, ry, rw, rh, count ? sum / count : 0.0, maxNits);
    }
    fclose(f);
    ID3D11DeviceContext_Unmap(context, (ID3D11Resource *)staging, 0);
    ID3D11Texture2D_Release(staging);
    ID3D11Texture2D_Release(frame);
    IDXGIResource_Release(resource);
    IDXGIOutputDuplication_ReleaseFrame(dup);
    IDXGIOutputDuplication_Release(dup);
    ID3D11DeviceContext_Release(context);
    ID3D11Device_Release(device);
    IDXGIOutput_Release(output);
    IDXGIAdapter1_Release(adapter);
    IDXGIFactory1_Release(factory);
    return 0;
}
