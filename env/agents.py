# env/agents.py
import random

class DariusGrinder:
    def act(self, market):
        # sells ore he "farmed", buys nothing
        qty = 5 + random.randint(0,3)
        market.sell("ore", qty)
        return {"sell_ore": qty}

class LuluCasual:
    def act(self, market):
        # buys 1 potion if price is not crazy
        if market.prices["potion"] < 35:
            market.buy("potion", 1)
            return {"buy_potion": 1}
        return {}

class YasuoSpeculator:
    def __init__(self):
        self.potion_stock = 0
    def act(self, market):
        p = market.prices["potion"]
        if p < 22:
            market.buy("potion", 2); self.potion_stock += 2
            return {"buy_potion": 2}
        if p > 30 and self.potion_stock > 0:
            market.sell("potion", self.potion_stock)
            sold = self.potion_stock; self.potion_stock = 0
            return {"sell_potion": sold}
        return {}
