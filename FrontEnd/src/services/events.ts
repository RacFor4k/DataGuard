export async function AddEvent(name: string, message: string, type: string, args: Array<any> | null = null) {
    console.log(type)
    const event = new CustomEvent(name, {
        detail:{
            message: message,
            type: type,
            ...(args !== null && {args:args})
        }
    });

    window.dispatchEvent(event);
}
