// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

class AltitudeHoldClient {
    // References to all the UI html elements
    UIElements = {
        statusContainer: undefined,
        statusText: undefined,
        lastUpdated: undefined,
        simulationRunning: undefined,
        altitudeValue: undefined,
        throttleValue: undefined,
        airspeedValue: undefined,
        elevatorTrimValue: undefined,
        aileronTrimValue: undefined,
        altitudeHoldActive: undefined,
        altitudeHoldTarget: undefined,
        targetClimbAngleValue: undefined,
        climbAngleValue: undefined,
        elevatorTrimKp: undefined,
        elevatorTrimKi: undefined,
        elevatorTrimKd: undefined,
        elevatorTrimProportionalTerm: undefined,
        elevatorTrimIntegralTerm: undefined,
        elevatorTrimDerivativeTerm: undefined,
        aileronTrimKp: undefined,
        aileronTrimKi: undefined,
        aileronTrimKd: undefined,
        aileronTrimProportionalTerm: undefined,
        aileronTrimIntegralTerm: undefined,
        aileronTrimDerivativeTerm: undefined,
        altitudeGraph: undefined,
        climbAngleGraph: undefined,
    };

    altitudeData = [];
    climbAngleData = [];
    maxDataPoints = 100;

    constructor() {
        // Initialize the UIElements object with references to the corresponding DOM elements.
        // This is based on the id convention where the id of each element is the camelCase name of the property converted to kebab-case.
        Object.keys(this.UIElements).forEach((element) => {
            this.UIElements[element] = document.getElementById(
                element.replace(/([A-Z])/g, "-$1").toLowerCase(),
            );
        });

        // Verify that all required DOM elements are present.
        const missingElements = Object.entries(this.UIElements)
            .filter(([_, element]) => !element)
            .map(([name, _]) => name);

        if (missingElements.length > 0) {
            console.error(
                "Missing required DOM elements:",
                missingElements.join(", "),
            );
        }

        // Add event listeners to the checkbox for the altitude hold active/inactive state
        this.UIElements.altitudeHoldActive.addEventListener("change", (e) =>
            this.setAltitudeHoldActive(e),
        );

        // Add an event listener to the target altitude input field
        this.UIElements.altitudeHoldTarget.addEventListener("change", (e) =>
            this.setAltitudeHoldTarget(e),
        );

        // Add event listeners to the PID parameter input fields
        [
            this.UIElements.elevatorTrimKd,
            this.UIElements.elevatorTrimKi,
            this.UIElements.elevatorTrimKp,
            this.UIElements.aileronTrimKd,
            this.UIElements.aileronTrimKi,
            this.UIElements.aileronTrimKp,
        ].forEach((element) => {
            if (element) {
                element.addEventListener("change", (e) => {
                    let controller = element.id.includes("elevator") ? "elevatorTrim" : "aileronTrim";

                    const kp = parseFloat(
                        this.UIElements[`${controller}Kp`].value,
                    );
                    const ki = parseFloat(
                        this.UIElements[`${controller}Ki`].value,
                    );
                    const kd = parseFloat(
                        this.UIElements[`${controller}Kd`].value,
                    );
                    this.setPIDParameters(controller, kp, ki, kd);
                });

                // Force the element to be focused when the user changes the value with the arrows
                element.addEventListener("input", (e) => {
                    element.focus();
                });
            }
        });
    }

    // Start polling the server for the SimConnect connection status and Altitude Hold state.
    start() {
        // Start polling the server for the SimConnect connection status every 2 seconds.
        this.refreshStatus();
        window.setInterval(() => this.refreshStatus(), 2000);

        // Start polling the server for the Altitude Hold state every 250 milliseconds.
        this.refreshState();
        window.setInterval(() => this.refreshState(), 250);
    }

    // Poll the server for the SimConnect connection status
    async refreshStatus() {
        try {
            const response = await fetch("/Home/SimConnectStatus", {
                method: "GET",
                cache: "no-store",
            });

            if (!response.ok) return;

            const data = await response.json();
            this.applyStatus(data);
        } catch {
            // Keep the last known status if polling fails.
        }
    }

    // Poll the server for the Altitude Hold
    async refreshState() {
        try {
            const response = await fetch("/Home/AltitudeHoldState", {
                method: "GET",
                cache: "no-store",
            });

            if (!response.ok) return;

            const data = await response.json();
            this.applyAltitudeHoldState(data);
        } catch {
            // Keep the last known state if polling fails.
        }
    }

    // Update the UI with the SimConnect connection status and the last updated timestamp.
    applyStatus(simconnectStatus) {
        this.UIElements.statusContainer.dataset.connected = String(
            simconnectStatus.isConnected,
        );
        this.UIElements.statusText.textContent = simconnectStatus.isConnected
            ? "Connected"
            : "Disconnected";
        this.UIElements.simulationRunning.textContent =
            simconnectStatus.simulationRunning ? "Yes" : "No";

        if (simconnectStatus.lastUpdatedUtc) {
            const date = new Date(simconnectStatus.lastUpdatedUtc);
            if (!Number.isNaN(date.getTime())) {
                this.UIElements.lastUpdated.textContent = date.toLocaleString();
            }
        }
    }

    // Update the UI with the Altitude Hold state received from the server.
    applyAltitudeHoldState(altitudeHoldState) {
        this.UIElements.altitudeValue.textContent =
            altitudeHoldState.altitude.toFixed(2);
        this.UIElements.throttleValue.textContent =
            altitudeHoldState.throttlePosition.toFixed(1) + "%";
        this.UIElements.elevatorTrimValue.textContent =
            altitudeHoldState.elevatorTrimPosition.toFixed(2) + " degrees";
        this.UIElements.aileronTrimValue.textContent =
            altitudeHoldState.aileronTrimPosition.toFixed(2) + " degrees";
        this.UIElements.airspeedValue.textContent =
            altitudeHoldState.airspeed.toFixed(2) + " knots";
        this.UIElements.targetClimbAngleValue.textContent =
            altitudeHoldState.targetClimbAngle.toFixed(2) + " degrees";
        this.UIElements.climbAngleValue.textContent =
            altitudeHoldState.climbAngle.toFixed(2) + " degrees";

        // Set the value of the checkbox and the target altitude input field based on the state received from the server.
        this.UIElements.altitudeHoldActive.checked = altitudeHoldState.isActive;
        if (document.activeElement !== this.UIElements.altitudeHoldTarget) {
            this.UIElements.altitudeHoldTarget.value =
                altitudeHoldState.targetAltitude.toFixed(2);
        }

        // Update the PID parameter input fields with the values received from the server, but only if the user is not currently editing them (i.e., they are not focused).
        if (
            document.activeElement !== this.UIElements.elevatorTrimKp &&
            document.activeElement !== this.UIElements.elevatorTrimKi &&
            document.activeElement !== this.UIElements.elevatorTrimKd
        ) {
            this.UIElements.elevatorTrimKp.value =
                altitudeHoldState.elevatorTrimPID.kp / 1e-4;
            this.UIElements.elevatorTrimKi.value =
                altitudeHoldState.elevatorTrimPID.ki / 1e-4;
            this.UIElements.elevatorTrimKd.value =
                altitudeHoldState.elevatorTrimPID.kd / 1e-4;
        }
        this.UIElements.elevatorTrimProportionalTerm.textContent =
            altitudeHoldState.elevatorTrimPID.proportionalTerm;
        this.UIElements.elevatorTrimIntegralTerm.textContent =
            altitudeHoldState.elevatorTrimPID.integralTerm;
        this.UIElements.elevatorTrimDerivativeTerm.textContent =
            altitudeHoldState.elevatorTrimPID.derivativeTerm;

        if (
            document.activeElement !== this.UIElements.aileronTrimKp &&
            document.activeElement !== this.UIElements.aileronTrimKi &&
            document.activeElement !== this.UIElements.aileronTrimKd
        ) {
            this.UIElements.aileronTrimKp.value =
                altitudeHoldState.aileronTrimPID.kp / 1e-4;
            this.UIElements.aileronTrimKi.value =
                altitudeHoldState.aileronTrimPID.ki / 1e-4;
            this.UIElements.aileronTrimKd.value =
                altitudeHoldState.aileronTrimPID.kd / 1e-4;
        }
        this.UIElements.aileronTrimProportionalTerm.textContent =
            altitudeHoldState.aileronTrimPID.proportionalTerm;
        this.UIElements.aileronTrimIntegralTerm.textContent =
            altitudeHoldState.aileronTrimPID.integralTerm;
        this.UIElements.aileronTrimDerivativeTerm.textContent =
            altitudeHoldState.aileronTrimPID.derivativeTerm;

        // Update the graphs with the new data
        this.altitudeData.push(altitudeHoldState.altitude);
        this.climbAngleData.push(altitudeHoldState.climbAngle);
        if (this.altitudeData.length > this.maxDataPoints) {
            this.altitudeData.shift();
            this.climbAngleData.shift();
        }

        this.drawGraph(
            this.UIElements.altitudeGraph,
            this.altitudeData,
            "Altitude",
            "blue",
            altitudeHoldState.targetAltitude - 1000,
            altitudeHoldState.targetAltitude + 1000,
            this.maxDataPoints
        );

        this.drawGraph(
            this.UIElements.climbAngleGraph,
            this.climbAngleData,
            "Climb Angle",
            "orange",
            altitudeHoldState.targetClimbAngle - 10,
            altitudeHoldState.targetClimbAngle + 10,
            this.maxDataPoints
        );
    }

    async setAltitudeHoldActive(event) {
        const isActive = event.target.checked;
        const url = isActive
            ? "/Home/ActivateAltitudeHold"
            : "/Home/DeactivateAltitudeHold";
        await fetch(url, { method: "POST" });
    }

    async setAltitudeHoldTarget(event) {
        const targetAltitude = parseFloat(event.target.value);
        await fetch(
            `/Home/SetTargetAltitude?targetAltitude=${targetAltitude}`,
            {
                method: "POST",
            },
        );
    }

    async setPIDParameters(controllerName, Kp, Ki, Kd) {
        await fetch(
            `/Home/SetPIDParameters?controllerName=${controllerName}&Kp=${Kp * 1e-4}&Ki=${Ki * 1e-4}&Kd=${Kd * 1e-4}`,
            {
                method: "POST",
            },
        );
    }

    drawGraph(canvas, data, label, color, min = 0, max = 100, maxDataPoints = 100) {
        const ctx = canvas.getContext("2d");
        if (!ctx) return;
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw reference line, assuming data is normalized between min and max
        const referenceValue = (min + max) / 2;
        const referenceY = canvas.height - ((referenceValue - min) / (max - min)) * canvas.height;
        ctx.beginPath();
        ctx.strokeStyle = "gray";
        ctx.setLineDash([5, 5]);
        ctx.moveTo(0, referenceY);
        ctx.lineTo(canvas.width, referenceY);
        ctx.stroke();
        ctx.setLineDash([]);

        // Draw the label
        ctx.fillStyle = color;
        ctx.font = "12px Arial";
        ctx.fillText(label, 10, 20);

        // Draw the min and max labels
        ctx.fillStyle = "black";
        ctx.fillText(`${min.toFixed(2)}`, 10, canvas.height - 10);
        ctx.fillText(`${max.toFixed(2)}`, 10, 30);

        // Draw a grid
        const gridSpacing = 50; // pixels
        ctx.strokeStyle = "lightgray";
        ctx.lineWidth = 1;
        for (let x = 0; x < canvas.width; x += gridSpacing) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, canvas.height);
            ctx.stroke();
        }

        // Draw the data line
        ctx.beginPath();
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        ctx.moveTo(0, canvas.height - ((data[0] - min) / (max - min)) * canvas.height);
        for (let i = 1; i < data.length; i++) {
            // Start drawing from the right. Equally divide the x data knowing the maximum number of data points.
            const x = (i / maxDataPoints) * canvas.width;
            const y = canvas.height - ((data[i] - min) / (max - min)) * canvas.height;
            ctx.lineTo(x, y);
        }
        ctx.stroke();
    }
}

(() => {
    const altitudeHoldClient = new AltitudeHoldClient();
    altitudeHoldClient.start();
})();
