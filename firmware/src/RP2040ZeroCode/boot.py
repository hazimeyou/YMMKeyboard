import usb_hid

# Vendor-defined custom HID (64-byte IN/OUT report, no report ID)
CUSTOM_HID_REPORT_DESCRIPTOR = bytes((
    0x06, 0x00, 0xFF,  # Usage Page (Vendor Defined 0xFF00)
    0x09, 0x01,        # Usage (0x01)
    0xA1, 0x01,        # Collection (Application)
    0x15, 0x00,        # Logical Minimum (0)
    0x26, 0xFF, 0x00,  # Logical Maximum (255)
    0x75, 0x08,        # Report Size (8)
    0x95, 0x40,        # Report Count (64)
    0x09, 0x01,        # Usage (0x01)
    0x81, 0x02,        # Input (Data,Var,Abs)
    0x95, 0x40,        # Report Count (64)
    0x09, 0x01,        # Usage (0x01)
    0x91, 0x02,        # Output (Data,Var,Abs)
    0xC0,              # End Collection
))

custom_hid = usb_hid.Device(
    report_descriptor=CUSTOM_HID_REPORT_DESCRIPTOR,
    usage_page=0xFF00,
    usage=0x0001,
    report_ids=(),
    in_report_lengths=(64,),
    out_report_lengths=(64,),
)

usb_hid.enable((
    usb_hid.Device.KEYBOARD,
    usb_hid.Device.MOUSE,
    usb_hid.Device.CONSUMER_CONTROL,
    custom_hid,
))
