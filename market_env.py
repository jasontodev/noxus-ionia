# env/market_env.py
from dataclasses import dataclass, field

@dataclass
class Item:
    name: str
    base_price: float

@dataclass
class Market:
    items: dict  # name -> Item
    prices: dict = field(default_factory=dict)
    inventory: dict = field(default_factory=dict)

    def __post_init__(self):
        for n, it in self.items.items():
            self.prices.setdefault(n, it.base_price)
            self.inventory.setdefault(n, 0)

    def buy(self, item, qty):
        # buyer pays price * qty; inventory decreases
        self.inventory[item] -= qty

    def sell(self, item, qty):
        # seller receives price * qty; inventory increases
        self.inventory[item] += qty

    def tick(self, demand, supply):
        # super simple price update: price += k*(demand - supply)
        k = 0.01
        for n in self.items:
            delta = k * (demand.get(n,0) - supply.get(n,0))
            self.prices[n] = max(0.01, self.prices[n] * (1 + delta))

def bootstrap_demo():
    items = {
        "gold": Item("gold", 1.0),
        "ore": Item("ore", 10.0),
        "potion": Item("potion", 25.0),
    }
    market = Market(items)
    # one fake step: more demand for potion than supply -> price should rise
    demand = {"potion": 100}
    supply = {"potion": 60}
    market.tick(demand, supply)
    print("Prices:", market.prices)

if __name__ == "__main__":
    bootstrap_demo()
