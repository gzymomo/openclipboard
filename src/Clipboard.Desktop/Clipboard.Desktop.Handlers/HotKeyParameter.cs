using System.Windows.Forms;
using Newtonsoft.Json;

namespace Clipboard.Desktop.Handlers;

internal class HotKeyParameter
{
	public int Control { get; set; }

	public Keys Vk { get; set; } = Keys.F1;


	public bool IsEnable { get; set; } = true;


	public static HotKeyParameter ToObject(string str)
	{
		return JsonConvert.DeserializeObject<HotKeyParameter>(str);
	}

	public override string ToString()
	{
		return JsonConvert.SerializeObject(new { Control, Vk, IsEnable });
	}
}
