using System.IO.Ports;
using System.Text;
using System.Diagnostics;

namespace YMMKeyboardPlugin;

public class SerialUidFinder
{
    public async Task<List<SerialDeviceInfo>> FindAllAsync()
    {
        var result = new List<SerialDeviceInfo>();

        foreach (var portName in SerialPort.GetPortNames())
        {
            try
            {
                using var port = new SerialPort(portName, 115200)
                {
                    Encoding = Encoding.ASCII,
                    NewLine = "\n",
                    ReadTimeout = 200,
                    WriteTimeout = 200,
                    DtrEnable = true,
                    RtsEnable = true,
                };

                port.Open();

                var sw = Stopwatch.StartNew();
                var buffer = new StringBuilder();

                // ★ 最大3秒待つ
                while (sw.ElapsedMilliseconds < 3000)
                {
                    try
                    {
                        var chunk = port.ReadExisting();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            buffer.Append(chunk);

                            foreach (var raw in buffer.ToString().Split('\n'))
                            {
                                var line = raw.Trim();
                                if (!line.StartsWith("UID:"))
                                    continue;

                                var uid = line.Substring(4).Trim();
                                if (uid.Length == 0)
                                    continue;

                                Debug.WriteLine($"[UID FOUND] {uid} @ {portName}");

                                result.Add(new SerialDeviceInfo
                                {
                                    PortName = portName,
                                    Uid = uid
                                });

                                sw.Stop();
                                goto NEXT_PORT;
                            }
                        }
                    }
                    catch (TimeoutException) { }

                    await Task.Delay(50);
                }

            NEXT_PORT:;
            }
            catch (UnauthorizedAccessException)
            {
                // 他アプリが掴んでいる → 無視
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SerialUidFinder] {portName}: {ex.Message}");
            }
        }

        return result;
    }
}
