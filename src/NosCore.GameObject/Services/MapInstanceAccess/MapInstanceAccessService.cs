﻿using NosCore.Data.StaticEntities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NosCore.Shared.Enumerations.Map;
using NosCore.Shared.I18N;

namespace NosCore.GameObject.Services.MapInstanceAccess
{
    public class MapInstanceAccessService
    {
        public readonly ConcurrentDictionary<Guid, MapInstance> MapInstances =
            new ConcurrentDictionary<Guid, MapInstance>();
        public MapInstanceAccessService(List<NpcMonsterDTO> npcMonsters, List<Map.Map> maps)
        {
            var mapPartitioner = Partitioner.Create(maps, EnumerablePartitionerOptions.NoBuffering);
            var mapList = new ConcurrentDictionary<short, Map.Map>();
            var npccount = 0;
            var monstercount = 0;
            Parallel.ForEach(mapPartitioner, new ParallelOptions { MaxDegreeOfParallelism = 8 }, map =>
            {
                var guid = Guid.NewGuid();
                map.Initialize();
                mapList[map.MapId] = map;
                var newMap = new MapInstance(map, guid, map.ShopAllowed, MapInstanceType.BaseMapInstance, npcMonsters);
                MapInstances.TryAdd(guid, newMap);
                newMap.LoadPortals();
                newMap.LoadMonsters();
                newMap.LoadNpcs();
                newMap.StartLife();
                monstercount += newMap.Monsters.Count;
                npccount += newMap.Npcs.Count;
            });
            maps.AddRange(mapList.Select(s => s.Value));
            Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.MAPNPCS_LOADED),
                npccount));
            Logger.Log.Info(string.Format(LogLanguage.Instance.GetMessageFromKey(LanguageKey.MAPMONSTERS_LOADED),
                monstercount));
        }

        public  Guid GetBaseMapInstanceIdByMapId(short mapId)
        {
            return MapInstances.FirstOrDefault(s =>
                s.Value?.Map.MapId == mapId && s.Value.MapInstanceType == MapInstanceType.BaseMapInstance).Key;
        }

        public  MapInstance GetMapInstance(Guid id)
        {
            return MapInstances.ContainsKey(id) ? MapInstances[id] : null;
        }
    }
}
