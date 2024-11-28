from graphviz import Digraph

# Create a UML sequence diagram for the microservice
uml = Digraph("Charts Microservice", format="png")
uml.attr(rankdir="TB", size="10,10", dpi="300")

# MAIN NODES
uml.node("Client", shape="box", style="filled", color="orange", label="Client Program")
uml.node("Microservice", shape="box", style="filled", color="green", label="Chart Microservice")
uml.node("Validator", shape="box", style="dashed", color="red", label="Input Validator", fontcolor='red')
uml.node("Matplotlib", shape="box", style="filled", color="lightblue", label="Matplotlib")
uml.node("Memory", shape="box", style="dotted", color="blue", label="Memory")

# ROUTES

# Client and MS
uml.edge("Client", "Microservice", xlabel="Start -> (Request Payload)", label="",
         color="black", constraint="false", fontsize="10", fontcolor='darkgreen')
uml.edge("Microservice", "Client", xlabel="End -> Status: Success", label="",
         color="black", constraint="false", fontsize="10", fontcolor='darkgreen')

# Validation flow
uml.edge("Microservice", "Validator", label="Validate Input", color="red", fontcolor="red")
uml.edge("Validator", "Microservice", label="Validation (Success/Failure)", fontcolor='red')

# Chart generation flow
uml.edge("Microservice", "Matplotlib", label="Generate Chart")
uml.edge("Matplotlib", "Memory", label="Encode .PNG and Store in Memory", color="blue")
uml.edge("Memory", "Microservice", label="Return Chart (Base64 Encoded)", color='blue')

# Client image decoding
uml.edge("Client", "Client", label="Decode Base64 Image for Display")

# Rendering diagram
uml.render("chart_microservice", view=False)
