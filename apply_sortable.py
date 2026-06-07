import os
import re

entity_dir = "/Users/akshaya/SmartWare/SmartInventory/SmartInventory.Core/Entities"

# Key properties we want to make sortable across all files
sortable_props = ["Name", "Email", "PoNumber", "InvoiceNumber", "Status", "Amount", "TotalAmount", "Rating", "SKU", "SellingPrice", "CostPrice", "FullName"]

for filename in os.listdir(entity_dir):
    if filename.endswith(".cs"):
        filepath = os.path.join(entity_dir, filename)
        with open(filepath, "r") as f:
            content = f.read()
            
        modified = False
        
        # Add namespace if not present
        if "using SmartInventory.Core.Attributes;" not in content and any(prop in content for prop in sortable_props):
            content = re.sub(r'(using System;.*?\n)', r'\1using SmartInventory.Core.Attributes;\n', content, count=1)
            
        # Add [Sortable] to specific properties if not already there
        for prop in sortable_props:
            pattern = r'(\s+)(public [A-Za-z0-9_\?]+ ' + prop + r' \{)'
            replacement = r'\1[Sortable]\1\2'
            if re.search(pattern, content) and "[Sortable]" not in re.findall(r'(\[Sortable\]\s+public [A-Za-z0-9_\?]+ ' + prop + r' \{)', content):
                content = re.sub(pattern, replacement, content)
                modified = True
                
        if modified:
            with open(filepath, "w") as f:
                f.write(content)
                
print("Applied [Sortable] to all core entities!")
