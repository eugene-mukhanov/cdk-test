async function main(event) {
    return {
        body: JSON.stringify(
            {receiptId: event.pathParameters.id, status: "PROCCESSED"} ),
        statusCode: 200,
    };
}

module.exports = {main};