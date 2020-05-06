
using Mirror;
using UnityEngine;

/// <summary>
/// Main API for despawning objects. If you ever need to despawn something, look here
/// </summary>
public static class Despawn
{

	/// <summary>
	/// Despawn the specified game object, syncing to all clients. Should only be called
	/// on networked objects (i.e. ones which have NetworkIdentity component). Despawning removes an
	/// object from the game, but may not necessarilly destroy it completely - it may end up back in the
	/// object pool to be later reused.
	/// </summary>
	/// <param name="toDespawn"></param>
	/// <param name="skipInventoryDespawn">If the indicated object is in inventory, it will
	/// be despawned via the inventory API instead. Set this to true to bypass this. This is only
	/// intended to be set to true for particular internal use cases in the lifecycle system, so should
	/// almost always be left at the default</param>
	/// <returns></returns>
	public static DespawnResult ServerSingle(GameObject toDespawn, bool skipInventoryDespawn = false)
	{
		return Server(DespawnInfo.Single(toDespawn));
	}

	/// <summary>
	/// Despawn the object server side and sync the despawn to all clients. Should only be
	/// called on networked objects (i.e. ones which have NetworkIdentity component). Despawning removes an
	/// object from the game, but may not necessarilly destroy it completely - it may end up back in the
	/// object pool to be later reused.
	/// </summary>
	/// <param name="info"></param>
	/// <param name="skipInventoryDespawn">If the indicated object is in inventory, it will
	/// be despawned via the inventory API instead. Set this to true to bypass this. This is only
	/// intended to be set to true for particular internal use cases in the lifecycle system, so should
	/// almost always be left at the default</param>
	/// <returns></returns>
	private static DespawnResult Server(DespawnInfo info, bool skipInventoryDespawn = false)
	{
		if (info == null)
		{
			Logger.LogError("Cannot despawn - info is null", Category.ItemSpawn);
			return DespawnResult.Fail(info);
		}

		if (!skipInventoryDespawn)
		{
			var pu = info.GameObject.GetComponent<Pickupable>();
			if (pu != null && pu.ItemSlot != null)
			{
				if (Inventory.ServerDespawn(pu.ItemSlot))
				{
					return DespawnResult.Single(info);
				}
				else
				{
					return DespawnResult.Fail(info);
				}
			}
		}

		//even if it has a pool prefab tracker, will still destroy it if it has no object behavior
		var poolPrefabTracker = info.GameObject.GetComponent<PoolPrefabTracker>();
		var transform = info.GameObject.GetComponent<IPushable>();
		var Electrical = info.GameObject.GetComponent<ElectricalOIinheritance>();
		//TODO: What's the purpose of this?
		if (Electrical != null)
		{
			if (!Electrical.InData.DestroyAuthorised)
			{
				Electrical.DestroyThisPlease();
				return DespawnResult.Single(info);
			}
		}

		_ServerFireDespawnHooks(DespawnResult.Single(info));

		if (transform != null)
		{
			transform.VisibleState = false;
		}

		if (poolPrefabTracker != null)
		{
			//pooled
			//destroy for all clients but keep it in server-side pool
			NetworkServer.UnSpawn(info.GameObject);
			if (!Spawn._TryAddToPool(info.GameObject))
			{
				//can't pool it, destroy
				Object.Destroy(info.GameObject);
			}
		}
		else
		{
			//not pooled - destroy everywhere
			NetworkServer.Destroy(info.GameObject);
		}


		return DespawnResult.Single(info);
	}

	/// <summary>
	/// Despawn the specified game object locally, on this client only. Should ONLY be called on non-networked objects
	/// (i.e. ones which don't have NetworkIdentity component).
	/// Despawning removes an object from the game, but may not necessarilly destroy it completely - it may end up back in the
	/// object pool to be later reused.
	/// </summary>
	/// <param name="toDespawn"></param>
	/// <returns></returns>
	public static DespawnResult ClientSingle(GameObject toDespawn)
	{
		return Client(DespawnInfo.Single(toDespawn));
	}

	/// <summary>
	/// Despawn the object locally, on this client only. Should ONLY be called on non-networked objects
	/// (i.e. ones which don't have NetworkIdentity component). Despawning removes an
	/// object from the game, but may not necessarilly destroy it completely - it may end up back in the
	/// object pool to be later reused.
	/// </summary>
	/// <param name="info"></param>
	/// <returns></returns>
	public static DespawnResult Client(DespawnInfo info)
	{
		if (info == null)
		{
			Logger.LogError("Cannot despawn - info is null", Category.ItemSpawn);
			return DespawnResult.Fail(info);
		}

		//add to pool
		if (Spawn._TryAddToPool(info.GameObject))
		{
			info.GameObject.SetActive(false);
		}
		else
		{
			Object.Destroy(info.GameObject);
		}

		return DespawnResult.Single(info);
	}

	/// <summary>
	/// Note - for internal use by spawn system only. Fires all server side despawn hooks
	/// </summary>
	/// <param name="result"></param>
	public static void _ServerFireDespawnHooks(DespawnResult result)
	{
		//fire server hooks
		var comps = result.GameObject.GetComponents<IServerDespawn>();
		if (comps != null)
		{
			foreach (var comp in comps)
			{
				comp.OnDespawnServer(result.DespawnInfo);
			}
		}
	}
}
