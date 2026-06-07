import os
import re

controllers_dir = '/Users/akshaya/SmartWare/SmartInventory/SmartInventory.API/Controllers'
files = [f for f in os.listdir(controllers_dir) if f.endswith('.cs')]

for file in files:
    path = os.path.join(controllers_dir, file)
    with open(path, 'r') as f:
        content = f.read()
    
    # Skip AuthController and ReportsController as they already have specific rate limiters
    if file in ['AuthController.cs', 'ReportsController.cs']:
        continue

    # Skip Transfers and PurchaseOrders which we just did
    if file in ['TransfersController.cs', 'PurchaseOrdersController.cs']:
        continue
        
    original_content = content
    
    # Add using Microsoft.AspNetCore.RateLimiting; if not present and there are mutations
    if re.search(r'\[(HttpPost|HttpPut|HttpDelete)', content):
        if 'using Microsoft.AspNetCore.RateLimiting;' not in content:
            # Add it after the first using
            content = re.sub(r'^(using .*?;)', r'\1\nusing Microsoft.AspNetCore.RateLimiting;', content, count=1)
            
        # Add [EnableRateLimiting("mutations")] above HttpPost, HttpPut, HttpDelete
        # Ensure we don't add it twice
        content = re.sub(r'(?<!\[EnableRateLimiting\("mutations"\)\]\n)(\s*)(\[Http(?:Post|Put|Delete).*?\])', r'\1[EnableRateLimiting("mutations")]\1\2', content)

    if content != original_content:
        with open(path, 'w') as f:
            f.write(content)
        print(f"Updated {file}")
