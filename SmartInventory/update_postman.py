import json

with open('SmartWare.postman_collection.json', 'r') as f:
    data = json.load(f)

for item in data.get('item', []):
    for subitem in item.get('item', []):
        if subitem.get('name') == "Register Supplier (Self)":
            subitem['request']['body']['raw'] = json.dumps({
                "name": "ABC Technologies Pvt Ltd",
                "gstin": "33AABCT1234C1Z5",
                "pan": "AABCT1234C",
                "address": "45 IT Park, Madurai",
                "contactFullName": "Raj Kumar",
                "email": "sales@abctech.com",
                "phone": "+919876543210",
                "password": "SecurePass123!"
            }, indent=2)
        elif subitem.get('name') == "Invite Supplier":
            subitem['request']['body']['raw'] = json.dumps({
                "name": "XYZ Pvt Ltd",
                "gstin": "33XYZCT1234C1Z5",
                "email": "contact@xyz.com",
                "phone": "+919876543210"
            }, indent=2)
        elif subitem.get('name') == "Complete Registration (Invite Flow)":
            subitem['request']['body']['raw'] = json.dumps({
                "inviteToken": "{{invite_token}}",
                "contactFullName": "Mohan Das",
                "jobTitle": "Director",
                "pan": "AABCT1234C",
                "address": "45 IT Park, Madurai",
                "password": "SecurePass123!"
            }, indent=2)

with open('SmartWare.postman_collection.json', 'w') as f:
    json.dump(data, f, indent=4)
