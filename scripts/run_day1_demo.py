# scripts/run_day1_demo.py
from env.market_env import Market, Item
from env.agents import DariusGrinder, LuluCasual, YasuoSpeculator

items = {"gold": Item("gold",1.0), "ore": Item("ore",10.0), "potion": Item("potion",25.0)}
m = Market(items)
agents = [DariusGrinder(), LuluCasual(), YasuoSpeculator()]

for t in range(20):
    demand = {}; supply = {}
    for a in agents:
        log = a.act(m)
        # naive demand/supply accumulator for price update
        for k,v in log.items():
            if "buy" in k:  demand.setdefault(k.split("_")[1],0); demand[k.split("_")[1]] += v
            if "sell" in k: supply. setdefault(k.split("_")[1],0); supply[k.split("_")[1]] += v
    m.tick(demand, supply)

print("Final prices:", m.prices)
