using System.Text.Json;

using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	private readonly string _commandEpoch = Guid.NewGuid ().ToString ("N");
	private long _commandCompleted;
	/// <summary>Gets the instance epoch and completed/pending charger-command counts for installed-driver validation.</summary>
	/// <value>A typed JSON snapshot with Epoch, Completed and Pending; completion records an outcome, not physical charging success.</value>
	[EntityProperty (Id = "commandActivity")]
	public string CommandActivity { get; private set => Update ("commandActivity", value, ref field); } = "";

	private void PublishCommandActivity () => CommandActivity = JsonSerializer.Serialize (new ActivitySnapshot
		{
		Epoch = _commandEpoch,
		Completed = _commandCompleted,
		Pending = _commands.Count + (_commandInProgress ? 1 : 0)
		});
	private void CancelQueuedCommands ()
		{
		_commandCompleted += _commands.Count;
		_commands.Clear ();
		PublishCommandActivity ();
		}
	private sealed class ActivitySnapshot
		{
		public string Epoch { get; set; } = "";
		public long Completed
			{
			get; set;
			}
		public int Pending
			{
			get; set;
			}
		}
	}